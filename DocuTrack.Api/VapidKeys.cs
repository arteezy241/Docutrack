using WebPush;

namespace DocuTrack.Api
{
    public static class VapidKeys
    {
        public static void Generate()
        {
            var keys = VapidHelper.GenerateVapidKeys();
            Console.WriteLine("=== VAPID KEYS ===");
            Console.WriteLine("Public: " + keys.PublicKey);
            Console.WriteLine("Private: " + keys.PrivateKey);
            Console.WriteLine("==================");
        }
    }
}