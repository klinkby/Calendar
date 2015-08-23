using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AutoMapper;
using Klinkby.DataModel;
using Klinkby.Repository.Extensions;
using Klinkby.Repository.Properties;
using Klinkby.ViewModel;
using Booking = Klinkby.DataModel.Booking;
using Location = Klinkby.DataModel.Location;
using Role = Klinkby.ViewModel.Role;
using Service = Klinkby.DataModel.Service;
using Slot = Klinkby.DataModel.Slot;
using User = Klinkby.DataModel.User;
using System.Threading.Tasks;

namespace Klinkby.Repository
{
    public class BookingRepository : HomeRepository
    {
        private static readonly Service DummyService = new Service
            {
                Name = "Luxury Treatment",
                Description = "(you should add a service)",
                Price = 100,
            };

        static BookingRepository()
        {
            Mapper.CreateMap<DefaultPage, BookingPage>();
            Mapper.CreateMap<Location, ViewModel.Location>();
            Mapper.CreateMap<Service, ViewModel.Service>();
            Mapper.CreateMap<Slot, Slot>()
                  .ForMember(x => x.FromTime, o => o.MapFrom(x => BaseRepository.ToLocalTime(x.FromTime)))
                  .ForMember(x => x.FromTime, o => o.MapFrom(x => BaseRepository.ToLocalTime(x.ToTime)));                
            Mapper.CreateMap<DefaultPage, BookingConfirmPage>();
        }

        public BookingRepository(IUnitOfWork uow)
            : base(uow)
        {
        }

        public async Task<BookingPage> GetPageAsync(string path, long locationId, long serviceId, DateTime fromTime, string phone,
                                   bool edit)
        {
            DefaultPage defPage = await base.GetPageAsync(path, edit);
            if (defPage == null)
                return null;
            BookingPage page = Mapper.Map<DefaultPage, BookingPage>(defPage);
            page.LocationId = locationId;
            page.ServiceId = serviceId;
            Tenant t = _uow.Tenant;
            page.Locations = t.Locations
                              .Where(l => l.Deleted == null)
                              .OrderBy(l => l.Name)
                              .Select(Mapper.Map<Location, ViewModel.Location>)
                              .ToArray();
            page.Services = t.Services
                             .Where(s => s.Deleted == null)
                             .OrderBy(s => s.Name)
                             .Select(Mapper.Map<Service, ViewModel.Service>)
                             .ToArray();

            if (page.ServiceId == 0 && page.Services.Any())
                page.ServiceId = page.Services.First().ServiceId;
            if (page.LocationId == 0 && page.Locations.Any())
                page.LocationId = page.Locations.First().LocationId;
            page.Slots = GetSlots(page.LocationId, page.ServiceId, fromTime).ToArray();
            if (page.SlotId == 0 && page.Slots.Any())
            {
                TimeSlot slot = page.Slots.First().Entries.First();
                page.SlotId = slot.SlotId;
                page.Tick = slot.FromTime.Ticks;
            }
            page.FromTicks = fromTime.Ticks;
            page.Mobile = phone;
            return page;
        }

        public IEnumerable<WeekDay> GetSlots(long locationId, long serviceId, DateTime fromTime)
        {
            Tenant t = _uow.Tenant;
            TimeSpan duration = (t.Services
                                  .SingleOrDefault(s => s.ServiceId == serviceId) ?? DummyService)
                .ServiceDuration;
            DateTime toTime = fromTime.StartOfNextWeek();
            fromTime = fromTime.StartOfWeek();
            DateTime now = DateTime.UtcNow;
            if (fromTime < now)
                fromTime = now;
            IOrderedEnumerable<Slot> slots = from s in _uow.Tenant.Slots
                                             where s.FromTime >= fromTime
                                                   && s.ToTime <= toTime
                                                   && s.LocationId == locationId
                                                   && s.Duration >= duration
                                             orderby s.FromTime
                                             select s;
            IEnumerable<IGrouping<DateTime, TimeSlot>> entries = slots.SelectMany(s => s.GetAvailable()
                                                                                        .Where(
                                                                                            b => b.Duration >= duration))
                                                                      .GroupBy(a => a.FromTime.Date);
            return from day in entries let segments = day.SelectMany(e => e.GetSegments(duration)) select new WeekDay
                {
                    Date = day.Key.ToShortDateString(),
                    Name = GetCurrentUICultureDayName(day.Key.DayOfWeek),
                    Entries = segments.ToArray(),
                };
        }

        public static string GetCurrentUICultureDayName(DayOfWeek dayOfWeek)
        {
            var dateTimeFormatInfo = DateTimeFormatInfo.GetInstance(CultureInfo.CurrentUICulture);
            if (dateTimeFormatInfo != null)
            {
                string txt = dateTimeFormatInfo.GetDayName(dayOfWeek);
                return txt.Substring(0, 1).ToUpper(CultureInfo.CurrentCulture) + txt.Substring(1);
            }
            return dayOfWeek.ToString(); // Fallback
        }

        public long CreateBooking(BookingPage page, long userId, Guid activityId)
        {
            Tenant t = _uow.Tenant;
            Service svc = t.Services.Single(x => x.ServiceId == page.ServiceId);
            var fromTime = ToUtc(new DateTime(page.Tick, DateTimeKind.Local));
            Slot slot = t.Slots.SingleOrDefault(x => x.SlotId == page.SlotId);
            if (slot == null)
                throw new InvalidOperationException(Resources.SlotError);
            var b = new Booking
                {
                    FromTime = fromTime,
                    ToTime = fromTime + svc.ServiceDuration,
                    ServiceId = page.ServiceId,
                    SlotId = page.SlotId,
                    ActivityId = activityId,
                };
            if (!b.IsIn(slot))
                throw new InvalidOperationException(Resources.SlotError);
            if (slot.Bookings.Any(x => x.Overlaps(b)))
                throw new InvalidOperationException(Resources.BookingConflictError);
            string phone = page.MobileFormatted;

            User u = (userId == 0)
                     ? BaseRepository.FindSimilarUser(t, null, phone)
                     : t.Users.Single(x => x.UserId == userId);
            if (u != null && u.Deleted.HasValue)
            {
                throw new InvalidOperationException(Resources.UserDeletedError);
            }
            if (u == null)
            {
                u = CreateUser(page.Name, phone);
            }
            else
            {
                u.Name = page.Name;
                u.Phone = page.MobileFormatted;
            }
            b.User = u;
            _uow.Tenant.Bookings.Add(b);
            _uow.Save();
            return fromTime.Ticks;
        }

        private User CreateUser(string name, string phone)
        {
            Tenant t = _uow.Tenant;
            var u = new User
                {
                    Name = name,
                    Email = phone,
                    Phone = phone,
                    RoleId = (long) Role.Guest,
                };
            t.Users.Add(u);
            return u;
        }

        public async Task<BookingConfirmPage> GetConfirmationAsync(string path, long tick, long slotId, bool edit)
        {
            DefaultPage defPage = await base.GetPageAsync(path, edit);
            if (defPage == null)
                return null;
            BookingConfirmPage page = Mapper.Map<DefaultPage, BookingConfirmPage>(defPage);
            if (edit)
            {
                page.Booking = MyBooking.Test;
                page.UserPhone = "+4512345678";
            }
            else
            {
                Tenant t = _uow.Tenant;
                var fromTime = new DateTime(tick, DateTimeKind.Utc);
                Booking b = t.Bookings.Single(m => m.SlotId == slotId && m.FromTime == fromTime && !m.Deleted.HasValue);
                page.Booking = Mapper.Map<Booking, MyBooking>(b);
                page.Booking.FromTime = ToLocalTime(page.Booking.FromTime);
                page.UserPhone = b.User.Phone;
                double serviceVat;
                if (double.TryParse(this._set[SettingKeyName.ServiceVAT], out serviceVat))
                {
                    serviceVat /= 100;
                }
                page.Track = ToGaq(b, serviceVat);
            }
            return page;
        }
        
        private static IEnumerable<object[]> ToGaq(Booking booking, double serviceVatFactor)
        {
            string id = "B#" + booking.BookingId.ToString(CultureInfo.InvariantCulture);
            var service = booking.Service;
            var user = booking.User;
            yield return new object[]
                {
                    "_addTrans",
                    id, // transaction ID - required
                    booking.Tenant.Name, // affiliation or store name
                    service.Price, // total - required
                    service.Price * serviceVatFactor, // tax
                    0, // shipping
                    user.City, // city
                    user.District ?? string.Empty, // state or province
                    user.CountryId // country                    
                };
            yield return new object[]
                {
                    "_addItem",
                    id, // transaction ID - necessary to associate item with transaction
                    !string.IsNullOrEmpty(service.SKU) ? service.SKU : service.Name, // SKU/code - required
                    service.Name, // product name - necessary to associate revenue with product
                    booking.Slot.Location.Name, // category or variation
                    service.Price, // unit price - required
                    1 // quantity - required
                };
            yield return new object[]
                {
                    "_trackTrans"
                };
        }
    }
}