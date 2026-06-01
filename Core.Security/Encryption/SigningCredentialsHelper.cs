using Microsoft.IdentityModel.Tokens;

namespace NetCoreBackend.NArchitecture.Core.Security.Encryption;

public static class SigningCredentialsHelper
{
    public static SigningCredentials CreateSigningCredentials(SecurityKey securityKey)
    {
        // HmacSha512 ("HS512") is the JWT standard. HmacSha512Signature uses the XML DSig URI
        // (http://www.w3.org/2001/04/xmldsig-more#hmac-sha512) which strict JWT validators in
        // other languages may reject during interop.
        return new(securityKey, SecurityAlgorithms.HmacSha512);
    }
}
