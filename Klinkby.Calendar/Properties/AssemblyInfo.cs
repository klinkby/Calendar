using System;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Klinkby.Calendar")]
[assembly: AssemblyDescription("General logic for handling time slots in a calendar.")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyCompany("Mads Klinkby")]
[assembly: AssemblyProduct("Klinkby")]
[assembly: AssemblyCopyright("Copyright © Mads Breusch Klinkby 2015")]
[assembly: NeutralResourcesLanguage("en-US")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: ComVisible(false)]
[assembly: CLSCompliant(true)]