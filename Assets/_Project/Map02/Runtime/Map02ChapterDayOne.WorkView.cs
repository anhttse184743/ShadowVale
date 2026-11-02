using ShadowVale.Gameplay.Combat;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        // The dock and post jobs are seen in the world (2026-10-25): the camera comes in on the work while the strip at the
        // bottom (Gui) carries the controls. The store keeps its full panel; its grid is the puzzle.
        float workViewClock;bool workViewActive;int hoveredBoard=-1;
        bool WorkShot(out Vector3 eye,out Vector3 focus,out float fov)
        {
            eye=focus=Vector3.zero;fov=46;
            if(dayActivity==DayActivity.Dock)
            {
                var dock=sites["dock"].position;
                if(DockHammering&&Stake!=null)
                {
                    // Side on to the swing: Nam in profile with the mallet's arc, chú Bảy beyond the stake facing us.
                    var stake=Stake.position;focus=new Vector3(stake.x,dock.y+1.05f,stake.z)-Vector3.forward*.45f;eye=focus+new Vector3(-2.6f,.65f,-.45f);fov=44;return true;
                }
                // Steep overhead view: the held plank and both bearings clear Nam in the foreground; screen-right remains world +X.
                focus=dock+new Vector3(.3f,.25f,1.15f);eye=dock+new Vector3(.3f,5.5f,-1.65f);return true;
            }
            if(dayActivity!=DayActivity.Lamp&&dayActivity!=DayActivity.Gate&&dayActivity!=DayActivity.Path)return false;
            int p=postIndex;fov=50;
            if(dayActivity==DayActivity.Lamp){var lamp=postLamps[p].position;focus=lamp+new Vector3(0,-.45f,.45f);eye=lamp+new Vector3(.35f,1.25f,-2.3f);}   // from inside the village, out is up the screen as on the dial
            else if(dayActivity==DayActivity.Gate){var gate=postGates[p].position;focus=gate+new Vector3(.35f,.62f,0);eye=gate+new Vector3(.2f,1.4f,-2.2f);fov=48;}
            else{var site=sites[p==0?"bamboo":"gate"].position;focus=site+new Vector3(-1,.1f,.4f);eye=focus+new Vector3(.6f,3.4f,-3.1f);}
            return true;
        }
        // A landing people go up and down every day is trodden bare. The south dock's repair spot sits at the edge of the
        // paddy, so the rice plants on it are taken out (in play only; the scene keeps its field), or the stakes, chú Bảy
        // squatting at them and the steps stand waist-deep in rice.
        public int DockGrassHidden{get;private set;}
        void ClearDockGrass()
        {
            var spot=sites["dock"].position+new Vector3(-.3f,0,.6f);const float clear=3f;
            foreach(var field in FindObjectsByType<VillageRiceInstances>(FindObjectsSortMode.None))
            {
                var keep=new System.Collections.Generic.List<Vector4>(field.plants.Length);var sizes=new System.Collections.Generic.List<float>(field.plants.Length);
                for(int i=0;i<field.plants.Length;i++)
                {
                    var p=field.plants[i];var at=field.transform.TransformPoint(new Vector3(p.x,p.y,p.z));
                    if(Vector3.ProjectOnPlane(at-spot,Vector3.up).sqrMagnitude<clear*clear){DockGrassHidden++;continue;}
                    keep.Add(p);if(i<field.sizes.Length)sizes.Add(field.sizes[i]);
                }
                if(keep.Count==field.plants.Length)continue;
                field.plants=keep.ToArray();field.sizes=sizes.Count==keep.Count?sizes.ToArray():System.Array.Empty<float>();field.Rebuild();
            }
        }
        void TickWorkView()
        {
            HoverDockBoard();
            if(!DayActivityActive||!WorkShot(out var eye,out var focus,out float fov)){EndWorkView();return;}
            if(!workViewActive){workViewActive=true;workViewClock=0;}
            workViewClock+=Time.deltaTime;
            // Pulled in front of a wall or a post in the way, as the scene camera is; the work itself never blocks it.
            var ray=eye-focus;float reach=ray.magnitude;
            foreach(var hit in Physics.SphereCastAll(focus,.12f,ray.normalized,reach,~0,QueryTriggerInteraction.Ignore))
                if(hit.distance>.65f&&hit.collider.GetComponentInParent<Health>()==null&&!hit.transform.IsChildOf(dayProps))reach=Mathf.Min(reach,Mathf.Max(.7f,hit.distance-.1f));
            eye=focus+ray.normalized*reach;
            rig.SetCinematicView(eye,Quaternion.LookRotation(focus-eye),fov,Mathf.SmoothStep(0,1,workViewClock/.45f));
        }
        void EndWorkView(){if(!workViewActive)return;workViewActive=false;rig.ClearCinematicView();}
        /// <summary>The board in hand is held over the step it would go on, at the offset and turn being tried.</summary>
        void HoverDockBoard()
        {
            int board=dayActivity==DayActivity.Dock&&Day.stakeHits>=6&&selectedBoard>=0&&selectedBoard<physicalBoards.Count&&!System.Array.Exists(Day.boards,p=>p.x==selectedBoard)?selectedBoard:-1;
            if(board!=hoveredBoard){hoveredBoard=board;SyncDayProps();}   // the last one back on the stack
            if(board<0)return;
            physicalBoards[board].SetPositionAndRotation(sites["dock"].position+new Vector3(dockOffset,.36f+.012f*Mathf.Sin(Time.time*3),1.05f+dockSlot*.3f),Quaternion.Euler(0,selectionRotated?90:0,0));
        }
    }
}
