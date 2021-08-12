namespace v00v.Services.Database
{
    public class ContextFactory : IContextFactory
    {
        #region Methods

        public VideoContext CreateVideoContext()
        {
            return new();
        }

        #endregion
    }
}
