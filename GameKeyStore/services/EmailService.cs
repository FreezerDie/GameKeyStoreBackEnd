using System.Text;
using System.Text.Json;
using GameKeyStore.Models;

namespace GameKeyStore.Services
{
    public class ZeptoMailOptions
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
    }

    public class EmailService
    {
        private readonly HttpClient _httpClient;
        private readonly ZeptoMailOptions _options;
        private readonly ILogger<EmailService> _logger;

        public EmailService(HttpClient httpClient, IConfiguration configuration, ILogger<EmailService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _options = new ZeptoMailOptions();
            configuration.GetSection("ZeptoMail").Bind(_options);
        }

        public async Task<bool> SendOrderConfirmationEmailAsync(string recipientEmail, string recipientName, OrderWithSubOrdersDto order)
        {
            try
            {
                // Create email template
                var htmlBody = CreateOrderConfirmationTemplate(recipientName, order);
                var subject = $"Order Confirmation #{order.Id} - GameKeyStore";

                return await SendEmailAsync(recipientEmail, recipientName, subject, htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending order confirmation email to {Email}", recipientEmail);
                return false;
            }
        }

        private async Task<bool> SendEmailAsync(string recipientEmail, string recipientName, string subject, string htmlBody)
        {
            try
            {
                var emailRequest = new
                {
                    from = new { address = _options.FromAddress, name = _options.FromName },
                    to = new[]
                    {
                        new
                        {
                            email_address = new { address = recipientEmail, name = recipientName }
                        }
                    },
                    subject = subject,
                    htmlbody = htmlBody
                };

                var jsonContent = JsonSerializer.Serialize(emailRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Add authorization header
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Zoho-enczapikey {_options.ApiKey}");

                var response = await _httpClient.PostAsync(_options.ApiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Email sent successfully to {Email}. Response: {Response}", recipientEmail, responseContent);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send email to {Email}. Status: {Status}, Error: {Error}", 
                        recipientEmail, response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending email to {Email}", recipientEmail);
                return false;
            }
        }

        private string CreateOrderConfirmationTemplate(string customerName, OrderWithSubOrdersDto order)
        {
            var totalAmount = (order.TotalPrice / 100.0).ToString("F2");
            var orderDate = DateTime.UtcNow.ToString("MMMM dd, yyyy");
            
            var itemsHtml = new StringBuilder();
            if (order.SubOrders != null)
            {
                foreach (var subOrder in order.SubOrders)
            {
                var price = subOrder.Price?.ToString("F2") ?? "0.00";
                var gameName = subOrder.Game?.Name ?? "Unknown Game";
                var gameKeyCode = subOrder.GameKey?.Key ?? "N/A";
                var platform = "PC"; // Default platform since GameDto doesn't have Platform property

                itemsHtml.AppendLine($@"
                    <tr style=""border-bottom: 1px solid #eee;"">
                        <td style=""padding: 15px 0; vertical-align: top;"">
                            <strong style=""color: #333; font-size: 16px;"">{gameName}</strong><br>
                            <span style=""color: #666; font-size: 14px;"">Platform: {platform}</span><br>
                            <span style=""color: #666; font-size: 14px;"">Key: {gameKeyCode}</span>
                        </td>
                        <td style=""padding: 15px 0; text-align: right; vertical-align: top;"">
                            <strong style=""color: #333; font-size: 16px;"">${price}</strong>
                        </td>
                    </tr>
                ");
            }
            }

            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset=""UTF-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>Order Confirmation</title>
            </head>
            <body style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background-color: #f4f4f4;"">
                <div style=""max-width: 600px; margin: 20px auto; background-color: white; border-radius: 10px; box-shadow: 0 0 20px rgba(0,0,0,0.1); overflow: hidden;"">
                    
                    <!-- Header -->
                    <div style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center;"">
                        <h1 style=""margin: 0; font-size: 28px; font-weight: 700;"">GameKeyStore</h1>
                        <p style=""margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;"">Thank you for your purchase!</p>
                    </div>

                    <!-- Order Details -->
                    <div style=""padding: 30px;"">
                        <div style=""text-align: center; margin-bottom: 30px;"">
                            <h2 style=""color: #333; margin: 0 0 10px 0; font-size: 24px;"">Order Confirmed!</h2>
                            <p style=""color: #666; margin: 0; font-size: 16px;"">Hi {customerName}, your order has been successfully processed.</p>
                        </div>

                        <!-- Order Info -->
                        <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 30px;"">
                            <div style=""display: flex; justify-content: space-between; margin-bottom: 10px;"">
                                <span style=""font-weight: 600; color: #333;"">Order Number:</span>
                                <span style=""color: #666;"">#{order.Id}</span>
                            </div>
                            <div style=""display: flex; justify-content: space-between; margin-bottom: 10px;"">
                                <span style=""font-weight: 600; color: #333;"">Order Date:</span>
                                <span style=""color: #666;"">{orderDate}</span>
                            </div>
                            <div style=""display: flex; justify-content: space-between;"">
                                <span style=""font-weight: 600; color: #333;"">Status:</span>
                                <span style=""color: #28a745; font-weight: 600; text-transform: capitalize;"">{order.Status}</span>
                            </div>
                        </div>

                        <!-- Items -->
                        <h3 style=""color: #333; margin-bottom: 20px; font-size: 20px;"">Your Game Keys</h3>
                        <table style=""width: 100%; border-collapse: collapse; margin-bottom: 30px;"">
                            {itemsHtml}
                        </table>

                        <!-- Total -->
                        <div style=""border-top: 2px solid #667eea; padding-top: 20px; text-align: right;"">
                            <div style=""font-size: 20px; font-weight: 700; color: #333;"">
                                Total: <span style=""color: #667eea;"">${totalAmount}</span>
                            </div>
                        </div>

                        <!-- Important Notes -->
                        <div style=""background-color: #fff3cd; border: 1px solid #ffeeba; border-radius: 8px; padding: 20px; margin-top: 30px;"">
                            <h4 style=""color: #856404; margin: 0 0 15px 0; font-size: 16px;"">Important Information:</h4>
                            <ul style=""color: #856404; margin: 0; padding-left: 20px; font-size: 14px;"">
                                <li style=""margin-bottom: 8px;"">Your game keys are shown above and are ready to use immediately</li>
                                <li style=""margin-bottom: 8px;"">Please save this email for your records</li>
                                <li style=""margin-bottom: 8px;"">Keys are non-refundable once revealed</li>
                                <li>If you have any issues, please contact our support team</li>
                            </ul>
                        </div>
                    </div>

                    <!-- Footer -->
                    <div style=""background-color: #f8f9fa; padding: 20px; text-align: center; color: #666; font-size: 14px;"">
                        <p style=""margin: 0 0 10px 0;"">Thank you for choosing GameKeyStore!</p>
                        <p style=""margin: 0;"">If you need help, contact us at support@game-key-store.shop</p>
                    </div>

                </div>
            </body>
            </html>";
        }
    }
}
