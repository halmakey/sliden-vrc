
using UdonSharp;
using VRC.SDKBase;

namespace Chikuwa.Sliden
{
    public class SlidenListener : UdonSharpBehaviour
    {

        public virtual void OnSlidenLoad(VRCUrl url)
        {
            /* NOP */
        }

        public virtual void OnSlidenReady(VRCUrl url, uint maxPage, uint page)
        {
            /* NOP */
        }

        public virtual void OnSlidenError(SlidenError error)
        {
            /* NOP */
        }

        public virtual void OnSlidenNavigatePage(uint page)
        {
            /* NOP */
        }

        public virtual void OnSlidenCanLoad()
        {
            /* NOP */
        }

        public virtual void OnSlidenScreenHiddenChanged(bool hidden)
        {
            /* NOP */
        }
    }
}
