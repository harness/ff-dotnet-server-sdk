namespace SignNuGetPkcs11;

using System.Text;
using Net.Pkcs11Interop.X509Store;

public class PinProvider : IPinProvider
{
    public GetPinResult GetTokenPin(Pkcs11X509StoreInfo storeInfo, Pkcs11SlotInfo slotInfo, Pkcs11TokenInfo tokenInfo)
    {
        if (tokenInfo.HasProtectedAuthenticationPath)
        {
            Console.Write("Please authenticate with external device");
            return new GetPinResult(cancel: false, pin: null);
        }

        string? pin = null;
        while (string.IsNullOrEmpty(pin))
        {
            Console.Write("Enter PIN: ");
            pin = Console.ReadLine();
        }

        return new GetPinResult(cancel: false, pin: Encoding.UTF8.GetBytes(pin));
    }

    public GetPinResult GetKeyPin(Pkcs11X509StoreInfo storeInfo, Pkcs11SlotInfo slotInfo, Pkcs11TokenInfo tokenInfo, Pkcs11X509CertificateInfo certificateInfo)
    {
        Console.WriteLine("Cancel request");
        return new GetPinResult(cancel: true, pin: null);
    }
}