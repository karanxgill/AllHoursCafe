using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace AllHoursCafe.API.Services
{
    public class PayUService
    {
        private readonly string _merchantKey;
        private readonly string _merchantSalt;
        private readonly string _payuBaseUrl;
        public string MerchantKey => _merchantKey;
        public PayUService(IConfiguration configuration)
        {
            _merchantKey = configuration["PayU:MerchantKey"] ?? string.Empty;
            _merchantSalt = configuration["PayU:MerchantSalt"] ?? string.Empty;
            _payuBaseUrl = configuration["PayU:BaseUrl"] ?? string.Empty;
        }

        public string GenerateHash(Dictionary<string, string> parameters)
        {
            // Hash sequence: key|txnid|amount|productinfo|firstname|email|||||||||||salt
            var hashString = $"{_merchantKey}|{parameters["txnid"]}|{parameters["amount"]}|{parameters["productinfo"]}|{parameters["firstname"]}|{parameters["email"]}|||||||||||{_merchantSalt}";
            return GetSha512Hash(hashString);
        }

        public bool VerifyHash(Dictionary<string, string> parameters, string receivedHash)
        {
            // Response hash sequence: salt|status|||||||||||email|firstname|productinfo|amount|txnid|key
            var hashString = $"{_merchantSalt}|{parameters["status"]}|||||||||||{parameters["email"]}|{parameters["firstname"]}|{parameters["productinfo"]}|{parameters["amount"]}|{parameters["txnid"]}|{_merchantKey}";
            var calculatedHash = GetSha512Hash(hashString);
            return string.Equals(calculatedHash, receivedHash, StringComparison.OrdinalIgnoreCase);
        }

        private string GetSha512Hash(string text)
        {
            using (SHA512 sha512 = SHA512.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(text);
                byte[] hashBytes = sha512.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public string GetPayUForm(Dictionary<string, string> parameters, string hash, bool autoSubmit = true, string buttonStyle = "")
        {
            // Prepare HTML form for POST to PayU
            StringBuilder sb = new StringBuilder();
            sb.Append($"<form id='payuForm' action='{_payuBaseUrl}' method='post'>");
            foreach (var param in parameters)
            {
                sb.Append($"<input type='hidden' name='{param.Key}' value='{param.Value}' />");
            }
            sb.Append($"<input type='hidden' name='hash' value='{hash}' />");

            // If buttonStyle is provided, use it for the submit button
            if (!string.IsNullOrEmpty(buttonStyle))
            {
                sb.Append($"<button type='submit' id='payuSubmitBtn' class='{buttonStyle}'><i class='fas fa-lock mr-2'></i> Pay Now</button>");
            }
            else
            {
                sb.Append("<input type='submit' value='Pay Now' id='payuSubmitBtn' class='btn btn-primary' />");
            }

            sb.Append("</form>");

            // Add auto-submit script if requested
            if (autoSubmit)
            {
                sb.Append("<script>setTimeout(function() { document.getElementById('payuForm').submit(); }, 1000);</script>");
            }

            return sb.ToString();
        }
    }
}
