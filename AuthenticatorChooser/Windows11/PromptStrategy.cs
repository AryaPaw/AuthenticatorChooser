using System.Windows.Automation;

namespace AuthenticatorChooser.Windows11;

public interface PromptStrategy {

    bool canHandleTitle(string? actualTitle);
    Task handleWindow(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown);

}