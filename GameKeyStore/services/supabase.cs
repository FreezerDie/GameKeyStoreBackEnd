using Supabase;

namespace GameKeyStore.Services
{
    public class SupabaseService
    {
        private readonly Client _supabaseClient;

        public SupabaseService(IConfiguration configuration)
        {
            // Load from environment variables (loaded from .env file)
            var url = Environment.GetEnvironmentVariable("SUPABASE_URL") 
                      ?? throw new InvalidOperationException("SUPABASE_URL environment variable is not set. Please check your .env file.");
            
            var key = Environment.GetEnvironmentVariable("SUPABASE_KEY") 
                      ?? throw new InvalidOperationException("SUPABASE_KEY environment variable is not set. Please check your .env file.");

            // Validate the URL format
            if (url.Contains("your-project") || key.Contains("your-anon-key"))
            {
                throw new InvalidOperationException("Please replace the placeholder values in your .env file with your actual Supabase URL and key.");
            }

            var options = new SupabaseOptions
            {
                AutoConnectRealtime = true
            };

            _supabaseClient = new Client(url, key, options);
        }

        public async Task InitializeAsync()
        {
            await _supabaseClient.InitializeAsync();
        }

        public Client GetClient()
        {
            return _supabaseClient;
        }
    }
}
