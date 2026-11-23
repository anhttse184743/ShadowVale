using UnityEngine;
using UnityEngine.AI;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        Map02NightWatchPreview nightPropPreview;
        readonly System.Collections.Generic.List<Vector3> nightWoodStart=new(),nightWoodStow=new();
        float nextNightPreviewFrame;
        Transform NightModel(string model, Vector3 position)
        {
            var prefab = Resources.Load<GameObject>("Map02DayOne/NightWatch/" + model + "Prop");
            if (prefab == null) throw new System.InvalidOperationException("Missing Blender night-watch prefab: " + model);
            var go = Instantiate(prefab, dayProps, false); go.name = "Inspect " + model; go.transform.position = position;
            return go.transform;
        }
        float NightGround(Vector3 position)
        {
            return NavMesh.SamplePosition(position, out var hit, 2, NavMesh.AllAreas) ? hit.position.y : position.y;
        }
        void BuildNightPost(Vector3 site, int index, Material wood, Material cloth)
        {
            // Gateways close the east approach to the plaza and the north village lane.
            var point=index==0?new Vector3(-55.8f,site.y,9.4f):new Vector3(-75.2f,site.y,21f);
            var outward=index==0?new Vector3(1,0,-.17f).normalized:Vector3.forward;
            var gate=NightModel("VillageGate",point);gate.rotation=Quaternion.LookRotation(outward);
            var lampPoint=point-gate.forward*.8f-gate.right*2.75f;lampPoint.y=NightGround(lampPoint)+.9f;
            var lamp=NightModel("ShieldedLamp",lampPoint);lamp.rotation=gate.rotation;postLamps.Add(lamp);
            gate.position = new Vector3(gate.position.x, NightGround(gate.position), gate.position.z);
            // The bamboo wings follow the ground on either side of the passage.
            foreach (var child in gate.GetComponentsInChildren<Transform>())
                if (child.name == "FenceWingLeft" || child.name == "FenceWingRight")
                {
                    var sample = gate.position + gate.right * (child.name.EndsWith("Left") ? -2.87f : 2.87f);
                    child.position += Vector3.up * Mathf.Clamp(NightGround(sample) - gate.position.y, -.25f, .25f);
                }
            postGates.Add(gate); postLatches.Add(gate.GetComponent<Map02NightWatchVisual>().bolt);
            var start=point-gate.forward*2.2f+gate.right*.5f;start.y=NightGround(start);
            var stow=point-gate.forward*2.2f-gate.right*2f;stow.y=NightGround(stow);nightWoodStart.Add(start);nightWoodStow.Add(stow);
            var bundle = Group("Inspect patrol path bundle " + index, start);
            for (int j = 0; j < 5; j++) Part(bundle, "Firewood", new Vector3(j*.1f-.2f,.11f,0),new Vector3(.09f,.16f,.7f),wood);
            postBundles.Add(bundle);
            var rack = Group("Bundle rack " + index, stow);
            Part(rack,"Storage shelf",new Vector3(0,.14f,0),new Vector3(.85f,.22f,.8f),cloth);
        }
        void SyncNightPost(int index)
        {
            var post = Day.posts[index];
            postLamps[index].GetComponent<Map02NightWatchVisual>().SetLamp(post.lampAngle, State.phase % 2 == 1);
            postGates[index].GetComponent<Map02NightWatchVisual>().SetGate(State.phase==1&&post.gateClosed, State.phase==1&&post.latchSeated);
            postBundles[index].position=post.bundleStowed?nightWoodStow[index]+Vector3.up*.2f:nightWoodStart[index];
        }
        void TickNightPropPreview()
        {
            if (dayActivity != DayActivity.Gate && dayActivity != DayActivity.Lamp)
            { nightPropPreview?.Hide(); return; }
            if (Time.unscaledTime < nextNightPreviewFrame) return;
            nextNightPreviewFrame = Time.unscaledTime + 1f / 60;
            nightPropPreview ??= new Map02NightWatchPreview();
            var source = (dayActivity == DayActivity.Gate ? postGates[postIndex] : postLamps[postIndex]).GetComponent<Map02NightWatchVisual>();
            nightPropPreview.Render(source);
        }
        void DrawNightModel(Rect rect, bool latch = false)
        {
            var image = latch ? nightPropPreview?.LatchImage : nightPropPreview?.Image;
            if (image != null) GUI.DrawTexture(rect, image, ScaleMode.ScaleToFit, false);
        }
    }
}
