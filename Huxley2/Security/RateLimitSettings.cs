namespace Huxley2.Security
{
    public sealed class RateLimitSettings
    {
        public bool Enabled { get; set; } = false;

        public BucketSettings Keyed { get; set; } = new BucketSettings();
        public BucketSettings Legacy { get; set; } = new BucketSettings();

        public sealed class BucketSettings
        {
            public int PermitLimit { get; set; } = 60;
            public int WindowSeconds { get; set; } = 60;
        }
    }
}
