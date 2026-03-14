using System.Net;
using System.Net.Mail;
using System.Net.Mime;

public class EmailService
{
    private readonly IConfiguration _configuration;
    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void SendViolationEmail(string toEmail, string imagePath, string location)
    {
        string senderEmail = _configuration["EmailSettings:SenderEmail"];
        string appPassword = _configuration["EmailSettings:AppPassword"];
        string senderName = _configuration["EmailSettings:SenderName"];

        try
        {
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(senderEmail, senderName);
            mail.To.Add(toEmail);
            mail.Subject = "⚠️ CẢNH BÁO: Phát hiện vi phạm an toàn lao động PPE";

            mail.IsBodyHtml = true;
            
            // Tạo Content-ID để nhúng ảnh
            string contentId = Guid.NewGuid().ToString();
            string htmlBody = $@"
                <div style='font-family: Arial; padding: 20px;'>
                    <h2 style='color: red;'>Phát hiện vi phạm không mặc đồ bảo hộ!</h2>
                    <p><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
                    <p><strong>Khu vực:</strong> {location}</p>
                    <p><strong>Hình ảnh camera:</strong></p>
                    <img src='cid:{contentId}' alt='Ảnh vi phạm' style='max-width:600px; border: 2px solid red;'/>
                </div>";

            AlternateView avHtml = AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html);

            // Nhúng ảnh vào email
            if (System.IO.File.Exists(imagePath))
            {
                LinkedResource inline = new LinkedResource(imagePath, MediaTypeNames.Image.Jpeg);
                inline.ContentId = contentId;
                avHtml.LinkedResources.Add(inline);
            }

            mail.AlternateViews.Add(avHtml);

            SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(senderEmail, appPassword),
                EnableSsl = true
            };

            smtp.Send(mail);
            Console.WriteLine("Đã gửi email cảnh báo thành công!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi gửi email: " + ex.Message);
        }
    }
}