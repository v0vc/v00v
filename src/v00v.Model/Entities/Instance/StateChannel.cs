using System;

namespace v00v.Model.Entities.Instance
{
    public class StateChannel : Channel
    {
        #region Constructors

        static StateChannel()
        {
            Instance = new StateChannel
            {
                Title = "        -=New=-",
                //Order = -1,
                Id = Guid.NewGuid().ToString(),
                //Thumbnail =
                //    Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAABmJLR0QA/wD/AP+gvaeTAAAACXBIWXMAABOvAAATrwFj5o7DAAAAB3RJTUUH4gkeBwweeXs2HgAAAQJJREFUWMPt1S9LBFEUhvFzl0UEwSqIxSJutmg12WWL1Q9gtAp+APELWBaTdoOaLSaLYDAZDBb/gGvQn1EY2Z3d2ZldwXniuefwvHC490bU/HdS0UGkiNiOCBFxlFIyttSYwrEfTjE9LvkMzvzmErNVy+dwrTc3mK9Kvog7+dxjqWz5Ch4NzhNWy5Kv49nwvGFjVPkm3hXnA1tF5Tv4NDpf2B3qgcGe8jlEI0/eREd1dNDsF6CFboUBumgNuop2ieJ2L09j0r9hs+DcVUQ8ZGoLEbE2rgAHKaWT7MqKBJj4CuoAdYB+AV77nL2U0J97Dc8jYj8iljP124i4KKG/puZv8A0soxwPt4ipIQAAAABJRU5ErkJggg=="),
                IsStateChannel = true,
                SubsCount = -512,
                ViewCount = -512,
                SubsCountDiff = -512,
                ViewCountDiff = -512,
                ItemsCount = -512,
                Timestamp = DateTimeOffset.MinValue
            };
        }

        #endregion

        #region Static Properties

        public static StateChannel Instance { get; }

        #endregion
    }
}
