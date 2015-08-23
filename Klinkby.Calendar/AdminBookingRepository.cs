using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using AutoMapper;
using Klinkby.DataModel;
using Klinkby.Repository.Extensions;
using Klinkby.Repository.Properties;
using Klinkby.ViewModel;
using Booking = Klinkby.ViewModel.Booking;
using Location = Klinkby.ViewModel.Location;
using Order = Klinkby.DataModel.Order;
using OrderLine = Klinkby.DataModel.OrderLine;
using OrderStatus = Klinkby.DataModel.OrderStatus;
using ProductVariant = Klinkby.DataModel.ProductVariant;
using Service = Klinkby.ViewModel.Service;
using Slot = Klinkby.DataModel.Slot;

namespace Klinkby.Repository
{
    public class AdminBookingRepository : AdminRepositoryBase
    {
        static AdminBookingRepository()
        {
            // Booking
            Mapper.CreateMap<DataModel.Booking, Booking>()
                  .ForMember(x => x.LocationName, y => y.MapFrom(m => m.Slot.Location.Name))
                  .ForMember(x => x.LocationId, y => y.MapFrom(m => m.Slot.Location.LocationId))
                  .ForMember(x => x.FromTime, y => y.MapFrom(m => BaseRepository.ToLocalTime(m.FromTime)))
                  .ForMember(x => x.ToTime, y => y.MapFrom(m => BaseRepository.ToLocalTime(m.ToTime)));
            Mapper.CreateMap<Slot, ViewModel.Slot>()
                  .ForMember(v => v.Date, d => d.MapFrom(m => BaseRepository.ToLocalTime(m.FromTime.Date)))
                  .ForMember(v => v.FromTime, d => d.MapFrom(m => BaseRepository.ToLocalTime(m.FromTime).TimeOfDay))
                  .ForMember(v => v.ToTime, d => d.MapFrom(m => BaseRepository.ToLocalTime(m.ToTime).TimeOfDay))
                  .ForMember(s => s.Bookings, opt => opt.ResolveUsing<BookingResolver>());
            Mapper.CreateMap<ViewModel.Slot, Slot>()
                  .ForMember(d => d.FromTime, v => v.MapFrom(m => BaseRepository.ToUtc(m.Date + m.FromTime)))
                  .ForMember(d => d.ToTime, v => v.MapFrom(m => BaseRepository.ToUtc(m.Date + m.ToTime)));
            Mapper.CreateMap<DataModel.Location, Location>();
            Mapper.CreateMap<Location, DataModel.Location>();
            Mapper.CreateMap<DataModel.Service, Service>();
            Mapper.CreateMap<Service, DataModel.Service>();
        }

        public AdminBookingRepository(IUnitOfWork uow)
            : base(uow)
        {
        }

        public long CreateOrderFromBooking(long bookingId, Func<long, Guid, string> genConfirmation, out Guid activityId)
        {
            Tenant t = _uow.Tenant;
            DataModel.Booking booking =
                t.Bookings.SingleOrDefault(b => b.BookingId == bookingId && !b.Orders.Any() && !b.Deleted.HasValue);
            if (null == booking)
                throw new InvalidOperationException("An active booking " + bookingId + " without an order was not found");
            activityId = booking.ActivityId ?? Guid.Empty;
            var productVariant = GetProductVariant(activityId, booking);
            Order order = CreateOrder(productVariant, booking);
            t.Orders.Add(order);
            long orderNo = 1;
            lock (ShopRepository.Sync)
            {
                Order lastOrder = t.Orders.OrderByDescending(x => x.OrderNo).Take(1).SingleOrDefault();
                if (lastOrder != null)
                {
                    orderNo = lastOrder.OrderNo + 1;
                }
                order.OrderNo = orderNo;
                _uow.Save();
            }
            string html = genConfirmation(order.OrderId, activityId);
            if (null == order.Confirmation)
                order.Confirmation = html;
            order.OrderStatus = OrderStatus.Pending;
            _uow.Save();
            return orderNo;
        }

        private ProductVariant GetProductVariant(Guid activityId, DataModel.Booking booking)
        {
            Tenant t = _uow.Tenant;
            string sku = booking.Service.SKU;
            if (string.IsNullOrEmpty(sku))
                throw new ActivityException("The booking service " + booking.Service.Name + " must have a SKU",
                                            activityId);
            ProductVariant productVariant = (from p in t.Products
                                             where !p.Deleted.HasValue
                                             let pv =
                                                 p.ProductVariants.SingleOrDefault(
                                                     x => sku == x.SKU && !x.Deleted.HasValue)
                                             where pv != null
                                             select pv).SingleOrDefault();
            if (null == productVariant)
                throw new ActivityException("Product variant with SKU=" + sku + " was not found", activityId);
            return productVariant;
        }

        #region Booking

        private static IEnumerable<TimeSlot> GetTimeSlots(Slot slot, bool allBookings)
        {
            DateTime fromTime = slot.FromTime;
            string locationName = slot.Location.Name;
            DataModel.Booking[] orderedBookings
                = slot.Bookings
                      .Where(s => allBookings || !s.Deleted.HasValue)
                      .OrderBy(b => b.FromTime)
                      .ToArray();
            foreach (DataModel.Booking booking in orderedBookings)
            {
                if (fromTime < booking.FromTime)
                {
                    yield return new TimeSlot
                        {
                            SlotId = slot.SlotId,
                            FromTime = BaseRepository.ToLocalTime(fromTime),
                            ToTime = BaseRepository.ToLocalTime(booking.FromTime),
                            LocationName = locationName
                        };
                }

                yield return Mapper.Map<DataModel.Booking, Booking>(booking);
                fromTime = booking.ToTime;
            }
            if (fromTime != slot.ToTime)
            {
                yield return new TimeSlot
                    {
                        SlotId = slot.SlotId,
                        FromTime = BaseRepository.ToLocalTime(fromTime),
                        ToTime = BaseRepository.ToLocalTime(slot.ToTime),
                        LocationName = locationName
                    };
            }
        }

        public CalendarWeek GetCalendar(DateTime weekDate, bool allBookings)
        {
            Tenant t = _uow.Tenant;
            DateTime fromDate = weekDate.StartOfWeek();
            TimeSpan minTime = TimeSpan.MaxValue;
            TimeSpan maxTime = TimeSpan.MinValue;
            var weekDays
                = Enumerable.Range(0, 7)
                            .Select(i =>
                                {
                                    DateTime dayStart = fromDate.AddDays(i);
                                    DateTime dayEnd = dayStart.AddDays(1);
                                    IOrderedEnumerable<Slot> daySlots = t.Slots
                                                                         .Where(s => s.FromTime >= dayStart
                                                                                     && s.ToTime <= dayEnd)
                                                                         .OrderBy(s => s.FromTime);
                                    var entries = daySlots.SelectMany(s => GetTimeSlots(s, allBookings))
                                                          .ToArray();
                                    // find min/max day start/end
                                    if (entries.Length != 0)
                                    {
                                        var dayMinTime = entries[0].FromTime.TimeOfDay;
                                        if (dayMinTime < minTime)
                                        {
                                            minTime = dayMinTime;
                                        }
                                        var dayMaxTime = entries[entries.Length - 1].ToTime.TimeOfDay;
                                        if (dayMaxTime > maxTime)
                                        {
                                            maxTime = dayMaxTime;
                                        }
                                    }
                                    return new WeekDay
                                        {
                                            Date = ToLocalTime(dayStart).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                            Name = dayStart.DayOfWeek.ToString().Substring(0, 3) + " " + dayStart.Day + ".",
                                            Entries = entries,
                                            Today = dayStart.Date == DateTime.Today
                                        };
                                })
                            .ToArray();
            if (minTime > maxTime) // empty calendar
            {
                minTime = new TimeSpan(9, 0, 0);
                maxTime = new TimeSpan(16, 0, 0);
            }
            var c = new CalendarWeek
            {
                WeekNo = ToLocalTime(weekDate).GetWeekNo(),
                WeekDays = weekDays,
                DayStarts = minTime,
                DayEnds = maxTime,
                DatePreviousWeek = ToLocalTime(weekDate).AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateNextWeek = ToLocalTime(weekDate).AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            };
            return c;
        }

        public Booking GetBooking(long bookingId)
        {
            var b = _uow.Tenant.Bookings.Single(x => bookingId == x.BookingId && !x.Deleted.HasValue);
            return Mapper.Map<DataModel.Booking, Booking>(b);
        }

        public void SaveBooking(long bookingId, TimeSpan fromTimeSpan)
        {
            var b = _uow.Tenant.Bookings.Single(x => bookingId == x.BookingId && !x.Deleted.HasValue);
            DateTime newFromTime = (b.FromTime.Date + fromTimeSpan).ToUniversalTime();
            DateTime newToTime = newFromTime + b.Duration;
            if (_uow.Tenant.Bookings.Any(x => x.Overlaps(b) && bookingId != x.BookingId))
                throw new InvalidOperationException(Resources.BookingConflictError);
            var slots = _uow.Tenant.Slots.Where(x => newFromTime>=x.FromTime && newToTime<=x.ToTime).ToArray();
            if (slots.Length != 1)
            {
                var newSlot = new ViewModel.Slot()
                    {
                        Date = newFromTime.Date,
                        FromTime = newFromTime.TimeOfDay,
                        ToTime = newFromTime.TimeOfDay + b.Duration,
                        LocationName = b.Slot.Location.Name
                    };
                SaveSlot(newSlot);
                slots = _uow.Tenant.Slots.Where(x => newFromTime >= x.FromTime && newToTime <= x.ToTime).ToArray();
            }
            if (slots.Length != 1)
            {
                throw new InvalidOperationException("Could not create slot for booking");
            }
            var slot = slots[0];
            b.SlotId = slot.SlotId;
            b.FromTime = newFromTime;
            b.ToTime = newToTime;            
            _uow.Save();
        }

        public BookingView GetBooking(DateTime weekDate, bool allBookings)
        {
            Tenant t = _uow.Tenant;
            var b = new BookingView
                {
                    PickedDate = weekDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Locations =
                        t.Locations.Where(x => allBookings || x.Deleted == null)
                         .Select(Mapper.Map<DataModel.Location, Location>)
                         .ToArray(),
                    Services =
                        t.Services.Where(x => allBookings || x.Deleted == null)
                         .Select(Mapper.Map<DataModel.Service, Service>)
                         .ToArray(),
                    CalendarPartial = GetCalendar(weekDate, allBookings),
                };
            return b;
        }

        public IEnumerable<Service> GetServices()
        {
            Tenant t = _uow.Tenant;
            var svcs = t.Services.Select(Mapper.Map<DataModel.Service, Service>);
            return svcs;
        }

        public Service GetService(long id)
        {
            Tenant t = _uow.Tenant;
            Service svc = id < 1
                              ? new Service()
                              : Mapper.Map<DataModel.Service, Service>(t.Services.Single(x => x.ServiceId == id));
            svc.AllSKUs =
                t.Products.SelectMany(p => p.ProductVariants.Select(pv => pv.SKU)).OrderBy(sku => sku).ToArray();
            return svc;
        }

        public ViewModel.Slot GetSlot(long id, DateTime dateTime)
        {
            Tenant t = _uow.Tenant;
            ViewModel.Slot slot = (id > 0)
                                      ? Mapper.Map<Slot, ViewModel.Slot>(t.Slots.Single(x => x.SlotId == id))
                                      : new ViewModel.Slot
                                          {
                                              Date = dateTime,
                                              FromTime = new TimeSpan(9, 0, 0),
                                              ToTime = new TimeSpan(17, 0, 0)
                                          }; // TODO move to settings
            slot.LocationNames = t.Locations.Where(x => !x.Deleted.HasValue).Select(x => x.Name);
            return slot;
        }

        public long SaveSlot(ViewModel.Slot slot)
        {
            if (slot.FromTime.Days != 0 || slot.FromTime.Days != 0 || slot.FromTime > slot.ToTime)
            {
                throw new InvalidOperationException("Time should be formatted HH:MM");
            }
            Tenant t = _uow.Tenant;
            DataModel.Location location = t.Locations.SingleOrDefault(x => x.Name == slot.LocationName)
                                          ?? new DataModel.Location {Name = slot.LocationName};
            if (location.LocationId < 1)
                t.Locations.Add(location);
            Slot dataSlot;
            if (slot.SlotId < 1)
            {
                dataSlot = Mapper.Map<ViewModel.Slot, Slot>(slot);
                Debug.Assert(dataSlot.FromTime.Date == dataSlot.ToTime.Date);
                if (dataSlot.ToTime < dataSlot.FromTime || dataSlot.ToTime.Date != dataSlot.FromTime.Date)
                    throw new InvalidOperationException("Time overflow error");
                // check for overlaps
                Slot slot1 = dataSlot;
                IEnumerable<Slot> overlapping = t.Slots.Where(
                    b => b.FromTime <= slot1.FromTime && b.ToTime > slot1.FromTime
                         || b.FromTime < slot1.ToTime && b.ToTime >= slot1.ToTime);
                if (overlapping.Any())
                    throw new InvalidOperationException("The time specified overlaps existing time slots.");
                Slot before =
                    t.Slots.SingleOrDefault(
                        x =>
                        x.ToTime == dataSlot.FromTime && x.Location.Name == slot.LocationName &&
                        x.ToTime.TimeOfDay != TimeSpan.Zero);
                Slot after =
                    t.Slots.SingleOrDefault(
                        x =>
                        x.FromTime == dataSlot.ToTime && x.Location.Name == slot.LocationName &&
                        x.FromTime.TimeOfDay != TimeSpan.Zero);
                if (before != null) // there is an existing slot that ends when the new slot starts
                {
                    Debug.Assert(before.FromTime.Date == before.ToTime.Date);
                    if (after != null) // the new slot joins two existing slots
                    {
                        foreach (DataModel.Booking ab in after.Bookings)
                        {
                            ab.Slot = before;
                        }
                        before.ToTime = after.ToTime;
                        dataSlot = before;
                        _uow.DeleteObject(after);
                    }
                    else
                    {
                        before.ToTime = dataSlot.ToTime;
                        dataSlot = before;
                    }
                }
                else
                {
                    if (after != null) // there is an existing slot that starts then the new slot ends
                    {
                        Debug.Assert(after.FromTime.Date == after.ToTime.Date);
                        after.FromTime = dataSlot.FromTime;
                        dataSlot = after;
                    }
                    else // no adjecent slots
                    {
                        t.Slots.Add(dataSlot);
                    }
                }
            }
            else
            {
                dataSlot = t.Slots.Single(x => x.SlotId == slot.SlotId);
                Mapper.Map(slot, dataSlot);
            }
            dataSlot.Location = location;
            Debug.Assert(dataSlot.FromTime.Date == dataSlot.ToTime.Date);
            if (dataSlot.ToTime < dataSlot.FromTime || dataSlot.ToTime.Date != dataSlot.FromTime.Date)
                throw new InvalidOperationException("Time overflow error");
            _uow.Save();
            return dataSlot.SlotId;
        }

        public void DeleteSlot(long id, DateTime fromTime, DateTime toTime)
        {
            Tenant t = _uow.Tenant;
            Slot slot = t.Slots.First(s => s.SlotId == id);
            fromTime = ToUtc(fromTime);
            toTime = ToUtc(toTime);
            IEnumerable<DataModel.Booking> bookings = slot.Bookings.Where(b => b.SlotId == id);
            var enumerable = bookings as DataModel.Booking[] ?? bookings.ToArray();
            if (enumerable.Any())
            {
                // check for bookings in the timepspan
                DataModel.Booking[] bookingsInBetween =
                    enumerable.Where(b => b.FromTime >= fromTime && b.ToTime <= toTime).ToArray();
                if (bookingsInBetween.Any(b => !b.Deleted.HasValue))
                {
                    throw new InvalidOperationException("There are bookings in the timespan you want to delete");
                }
                bool pendingChanges = false;
                if (bookingsInBetween.Any())
                {
                    // ok any remaining are marked as deleted. remove them for good with the timespan
                    foreach (DataModel.Booking deletedBooking in bookingsInBetween)
                    {
                        _uow.DeleteObject(deletedBooking);
                    }
                    pendingChanges = true;
                }
                IEnumerable<DataModel.Booking> bookingsToMove =
                    enumerable.Where(b => b.FromTime >= toTime && b.ToTime <= slot.ToTime);
                var toMove = bookingsToMove as DataModel.Booking[] ?? bookingsToMove.ToArray();
                if (toMove.Any())
                {
                    var newSlot = new Slot
                        {
                            FromTime = toTime,
                            ToTime = slot.ToTime,
                            Tenant = t,
                            LocationId = slot.LocationId,
                        };
                    t.Slots.Add(newSlot);
                    foreach (DataModel.Booking b in toMove)
                    {
                        b.Slot = newSlot;
                    }
                    pendingChanges = true;
                }
                if (pendingChanges)
                {
                    _uow.Save();
                }
                slot.ToTime = fromTime;
                if (slot.FromTime == slot.ToTime)
                {
                    _uow.DeleteObject(slot);
                }
            }
            else
            {
                _uow.DeleteObject(slot);
            }
            _uow.Save();
        }

        public IEnumerable<Location> GetLocations()
        {
            Tenant t = _uow.Tenant;
            var locs = t.Locations.Select(Mapper.Map<DataModel.Location, Location>);
            return locs;
        }

        public Location GetLocation(long id)
        {
            if (id < 1)
                return new Location();
            Tenant t = _uow.Tenant;
            Location loc = Mapper.Map<DataModel.Location, Location>(t.Locations.Single(x => x.LocationId == id));
            return loc;
        }

        public long SaveLocation(Location loc)
        {
            Tenant t = _uow.Tenant;
            DataModel.Location item;
            if (loc.LocationId < 1)
            {
                item = Mapper.Map<Location, DataModel.Location>(loc);
                t.Locations.Add(item);
            }
            else
            {
                item = t.Locations.Single(x => x.LocationId == loc.LocationId);
                Mapper.Map(loc, item);
            }
            _uow.Save();
            return item.LocationId;
        }

        public void DeleteLocation(long id)
        {
            Tenant t = _uow.Tenant;
            t.Locations.Single(x => x.LocationId == id).Deleted = DateTime.UtcNow;
            _uow.Save();
        }

        public long SaveService(Service svc)
        {
            Tenant t = _uow.Tenant;
            if (svc.ServiceId < 1)
            {
                DataModel.Service newItem = Mapper.Map<Service, DataModel.Service>(svc);
                t.Services.Add(newItem);
            }
            else
            {
                DataModel.Service item = t.Services.Single(x => x.ServiceId == svc.ServiceId);
                Mapper.Map(svc, item);
            }
            _uow.Save();
            return svc.ServiceId;
        }

        public void DeleteBooking(long id)
        {
            Tenant t = _uow.Tenant;
            var booking = t.Bookings.Single(x => x.BookingId == id);
            booking.Deleted = DateTime.UtcNow;
            // delete any references from orders
            foreach (var o in booking.Orders)
            {
                o.BookingId = null;
            }
            _uow.Save();
        }

        public void DeleteService(long id)
        {
            Tenant t = _uow.Tenant;
            t.Services.Single(x => x.ServiceId == id).Deleted = DateTime.UtcNow;
            _uow.Save();
        }

        #endregion

        #region class BookingResolver

        private class BookingResolver : ValueResolver<Slot, IEnumerable<Booking>>
        {
            protected override IEnumerable<Booking> ResolveCore(Slot source)
            {
                IEnumerable<Booking> bookings = source.Bookings
                                                      .Where(x => x.Deleted == null) // TODO handle allBookings
                                                      .Select(Mapper.Map<DataModel.Booking, Booking>);
                return bookings;
            }
        }

        #endregion

        private Order CreateOrder(ProductVariant productVariant, DataModel.Booking booking)
        {
            var orderLine = new OrderLine
                {
                    ProductVariantId = productVariant.ProductVariantId,
                    Price = productVariant.Price,
                    DiscountAsPercent = productVariant.DiscountAsPercent,
                    Quantity = 1,
                };
            var order = new Order
                {
                    UserId = booking.UserId,
                    ActivityId = booking.ActivityId,
                    BookingId = booking.BookingId,
                    OrderLines = new[] {orderLine},
                    OrderStatus = OrderStatus.Pending,
                    Total = productVariant.Price,
                };
            if (productVariant.Product.VAT)
            {
                double serviceVAT;
                if (double.TryParse(_set[SettingKeyName.ServiceVAT], out serviceVAT) &&
                    !serviceVAT.Equals(0.0))
                {
                    double vatFactor = serviceVAT/100;
                    order.VAT = productVariant.Price*vatFactor;
                }
            }
            return order;
        }

        public ShopOrderLine[] CreateCartFromBooking(long bookingId, out UserDetails client)
        {
            var t = _uow.Tenant;
            var b = t.Bookings.First(x => bookingId == x.BookingId && !x.Deleted.HasValue);
            var pv = GetProductVariant(b.ActivityId ?? Guid.Empty, b);
            var sol = ShopRepository.ToShopOrderLine(pv, 1);
            var list = new ShopOrderLine[] { sol };
            client = Mapper.Map<DataModel.User, UserDetails>(b.User);
            return list;
        }
    }
}