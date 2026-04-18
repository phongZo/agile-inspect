using Microsoft.InformationProtection;

namespace ClipboardMonitorPlugin.Code.MIP
{
    public class ConsentDelegateImplementation : IConsentDelegate
    {
        public Consent GetUserConsent(string url)
        {
            return Consent.Accept;
        }
    }
}
