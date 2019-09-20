namespace v00v.Model.Enums
{
    public enum AppStatus : byte
    {
        AppStarted = 0,
        PeriodicSyncStarted = 1,
        PeriodicSyncFinished = 2,
        DailySyncStarted = 3,
        DailySyncFinished = 4,
        AppClosed = 5,
        NoSync = 6,
        ChannelAdd = 7,
        ChannelEdited = 8,
        ChannelDeleted = 9,
        SyncPlaylistStarted = 10,
        SyncPlaylistFinished = 11,
        SyncWithoutPlaylistStarted = 12,
        SyncWithoutPlaylistFinished = 13
    }
}
