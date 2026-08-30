using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NLog;

namespace AuthenticatorChooser.Windows11;

internal interface INativePinFiller {
    bool TrySetPasswordValue(IntPtr windowHandle, IntPtr bstrPin);
}

[ExcludeFromCodeCoverage]
internal sealed class NativeUia: INativePinFiller {

    private static readonly Logger Logger = LogManager.GetLogger(typeof(NativeUia).FullName!);

    private const int UiaValuePatternId = 10002;
    private const int UiaIsPasswordPropertyId = 30019;
    private const int TreeScopeElementAndDescendants = 0x5;

    public static NativeUia Shared { get; } = new();

    public bool TrySetPasswordValue(IntPtr windowHandle, IntPtr bstrPin) {
        if (windowHandle == IntPtr.Zero || bstrPin == IntPtr.Zero) {
            return false;
        }

        try {
            IUIAutomation automation = (IUIAutomation) (object) new CUIAutomation();
            automation.CreatePropertyCondition(UiaIsPasswordPropertyId, true, out IUIAutomationCondition? isPassword);
            if (isPassword is null) {
                Logger.Warn("Native UIA could not build the IsPassword condition");
                return false;
            }

            foreach (IntPtr hwnd in EnumerateHwnds(windowHandle)) {
                if (TrySetOnWindow(automation, isPassword, hwnd, bstrPin)) {
                    return true;
                }
            }

            Logger.Warn("Native UIA found no password field under the FIDO dialog window");
            return false;
        } catch (Exception exception) when (exception is not OutOfMemoryException) {
            Logger.Warn("Native UIA SetValue for the PIN field failed ({message})", exception.Message);
            return false;
        }
    }

    private static bool TrySetOnWindow(IUIAutomation automation, IUIAutomationCondition isPassword, IntPtr hwnd, IntPtr bstrPin) {
        automation.ElementFromHandle(hwnd, out IUIAutomationElement? root);
        if (root is null) {
            return false;
        }

        root.FindFirst(TreeScopeElementAndDescendants, isPassword, out IUIAutomationElement? target);
        if (target is null) {
            return false;
        }

        Guid valuePatternIid = typeof(IUIAutomationValuePattern).GUID;
        target.GetCurrentPatternAs(UiaValuePatternId, ref valuePatternIid, out IntPtr patternPtr);
        if (patternPtr == IntPtr.Zero) {
            return false;
        }

        try {
            ((IUIAutomationValuePattern) Marshal.GetObjectForIUnknown(patternPtr)).SetValue(bstrPin);
            return true;
        } finally {
            Marshal.Release(patternPtr);
        }
    }

    private static List<IntPtr> EnumerateHwnds(IntPtr root) {
        List<IntPtr> hwnds = [root];
        EnumChildWindows(root, (hwnd, _) => {
            hwnds.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return hwnds;
    }

    private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    [ComImport]
    [Guid("ff48dba4-60ef-4201-aa87-54103eef594e")]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class CUIAutomation { }

    [ComImport]
    [Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomation {
        void CompareElements(IntPtr el1, IntPtr el2, [MarshalAs(UnmanagedType.Bool)] out bool areSame);
        void CompareRuntimeIds(IntPtr runtimeId1, IntPtr runtimeId2, [MarshalAs(UnmanagedType.Bool)] out bool areSame);
        void GetRootElement(out IUIAutomationElement? root);
        void ElementFromHandle(IntPtr hwnd, out IUIAutomationElement? element);
        void ElementFromPoint(Point pt, out IUIAutomationElement? element);
        void GetFocusedElement(out IUIAutomationElement? element);
        void GetRootElementBuildCache(IntPtr cacheRequest, out IUIAutomationElement? root);
        void ElementFromHandleBuildCache(IntPtr hwnd, IntPtr cacheRequest, out IUIAutomationElement? element);
        void ElementFromPointBuildCache(Point pt, IntPtr cacheRequest, out IUIAutomationElement? element);
        void GetFocusedElementBuildCache(IntPtr cacheRequest, out IUIAutomationElement? element);
        void CreateTreeWalker(IntPtr condition, out IntPtr walker);
        void get_ControlViewWalker(out IntPtr walker);
        void get_ContentViewWalker(out IntPtr walker);
        void get_RawViewWalker(out IntPtr walker);
        void get_RawViewCondition(out IntPtr condition);
        void get_ControlViewCondition(out IntPtr condition);
        void get_ContentViewCondition(out IntPtr condition);
        void CreateCacheRequest(out IntPtr cacheRequest);
        void CreateTrueCondition(out IntPtr condition);
        void CreateFalseCondition(out IntPtr condition);
        void CreatePropertyCondition(int propertyId, [MarshalAs(UnmanagedType.Struct)] object value, out IUIAutomationCondition? condition);
    }

    [ComImport]
    [Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationElement {
        void SetFocus();
        void GetRuntimeId(out IntPtr runtimeId);
        void FindFirst(int scope, IUIAutomationCondition condition, out IUIAutomationElement? found);
        void FindAll(int scope, IUIAutomationCondition condition, out IntPtr found);
        void FindFirstBuildCache(int scope, IUIAutomationCondition condition, IntPtr cacheRequest, out IntPtr found);
        void FindAllBuildCache(int scope, IUIAutomationCondition condition, IntPtr cacheRequest, out IntPtr found);
        void BuildUpdatedCache(IntPtr cacheRequest, out IntPtr updatedElement);
        void GetCurrentPropertyValue(int propertyId, out IntPtr value);
        void GetCurrentPropertyValueEx(int propertyId, [MarshalAs(UnmanagedType.Bool)] bool ignoreDefaultValue, out IntPtr value);
        void GetCachedPropertyValue(int propertyId, out IntPtr value);
        void GetCachedPropertyValueEx(int propertyId, [MarshalAs(UnmanagedType.Bool)] bool ignoreDefaultValue, out IntPtr value);
        void GetCurrentPatternAs(int patternId, ref Guid riid, out IntPtr patternObject);
    }

    [ComImport]
    [Guid("352ffba8-0973-437c-a61f-f64cafd81df9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationCondition { }

    [ComImport]
    [Guid("a94cd8b1-0844-4cd6-9d2d-640537ab39e9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationValuePattern {
        void SetValue(IntPtr bstr);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point {
        public int x;
        public int y;
    }

}
