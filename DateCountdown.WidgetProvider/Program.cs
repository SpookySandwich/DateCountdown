using DateCountdown.WidgetProvider;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using WinRT;

ComWrappersSupport.InitializeComWrappers(new DefaultComWrappers());

using CountdownWidgetProvider provider = new();
WidgetProviderFactory factory = new(provider);
Guid classId = WidgetProviderFactory.ClassId;
IntPtr factoryPointer = Marshal.GetComInterfaceForObject(factory, typeof(IClassFactory));

int registerResult = NativeMethods.CoRegisterClassObject(
    ref classId,
    factoryPointer,
    ClassContext.LocalServer,
    RegistrationClass.MultipleUse,
    out uint registrationCookie);

if (registerResult < 0)
{
    Marshal.ThrowExceptionForHR(registerResult);
}

try
{
    provider.WaitForExit();
}
finally
{
    NativeMethods.CoRevokeClassObject(registrationCookie);
    Marshal.Release(factoryPointer);
}

namespace DateCountdown.WidgetProvider
{
    [ComVisible(true)]
    internal sealed class WidgetProviderFactory : IClassFactory
    {
        public static readonly Guid ClassId = new("9805b76e-c976-4884-b984-f57bb5a594b7");

        private const int ClassENoAggregation = unchecked((int)0x80040110);

        private readonly CountdownWidgetProvider _provider;

        public WidgetProviderFactory(CountdownWidgetProvider provider)
        {
            _provider = provider;
        }

        public int CreateInstance(IntPtr outer, ref Guid interfaceId, out IntPtr instance)
        {
            instance = IntPtr.Zero;

            if (outer != IntPtr.Zero)
            {
                return ClassENoAggregation;
            }

            IntPtr providerInspectable = MarshalInspectable<Microsoft.Windows.Widgets.Providers.IWidgetProvider>.FromManaged(_provider, true);
            try
            {
                return Marshal.QueryInterface(providerInspectable, ref interfaceId, out instance);
            }
            finally
            {
                Marshal.Release(providerInspectable);
            }
        }

        public int LockServer(bool lockServer)
        {
            return 0;
        }
    }

    [ComImport]
    [Guid("00000001-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(IntPtr outer, ref Guid interfaceId, out IntPtr instance);

        [PreserveSig]
        int LockServer(bool lockServer);
    }

    [Flags]
    internal enum ClassContext : uint
    {
        LocalServer = 0x4
    }

    [Flags]
    internal enum RegistrationClass : uint
    {
        MultipleUse = 0x1
    }

    internal static partial class NativeMethods
    {
        [DllImport("ole32.dll")]
        internal static extern int CoRegisterClassObject(
            ref Guid classId,
            IntPtr unknown,
            ClassContext classContext,
            RegistrationClass flags,
            out uint registrationCookie);

        [DllImport("ole32.dll")]
        internal static extern int CoRevokeClassObject(uint registrationCookie);
    }
}
