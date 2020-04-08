namespace v00v.Services.Database
{
    public interface IContextFactory
    {
        #region Methods

        VideoContext CreateVideoContext();

        #endregion
    }
}
