using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace DocuTrack.Api.Services
{
    public class FileService
    {
        private readonly AmazonS3Client _s3;
        private readonly string _bucketName;
        private readonly string _publicUrl;

        public FileService(IConfiguration config)
        {
            var accessKey = config["R2:AccessKey"];
            var secretKey = config["R2:SecretKey"];
            var endpoint = config["R2:Endpoint"];
            _bucketName = config["R2:BucketName"] ?? "docutrack-files";
            _publicUrl = config["R2:PublicUrl"] ?? endpoint!;

            var s3Config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                
                AuthenticationRegion = "auto",
            };

            _s3 = new AmazonS3Client(accessKey, secretKey, s3Config);
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folder = "documents")
        {
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{folder}/{Guid.NewGuid()}{ext}";

            using var stream = file.OpenReadStream();

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = fileName,
                InputStream = stream,
                ContentType = file.ContentType,
            };

            await _s3.PutObjectAsync(request);

            return $"{_publicUrl}/{fileName}";
        }

        public async Task DeleteFileAsync(string fileUrl)
        {
            try
            {
                var key = fileUrl.Replace($"{_publicUrl}/", "");
                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                };
                await _s3.DeleteObjectAsync(request);
            }
            catch
            {
                // silently ignore delete errors
            }
        }

        public async Task<Stream> DownloadFileAsync(string fileUrl)
        {
            var key = fileUrl.Replace($"{_publicUrl}/", "");
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
            };
            var response = await _s3.GetObjectAsync(request);
            return response.ResponseStream;
        }
    }
}