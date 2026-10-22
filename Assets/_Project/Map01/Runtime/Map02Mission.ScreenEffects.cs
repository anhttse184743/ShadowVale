using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ShadowVale.UI.HUD;
namespace ShadowVale.Map01
{
    public sealed partial class Map02Mission
    {
        Canvas characterEffectsCanvas;
        Image characterCrosshair;
        public LowHealthEffect CharacterLowHealth {get;private set;}
        public bool CharacterCrosshairVisible=>characterCrosshair!=null&&characterCrosshair.enabled&&characterEffectsCanvas.enabled;
        void InitializeCharacterEffects()
        {
            var host=new GameObject("Map 2 character screen effects",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));host.layer=5;host.transform.SetParent(transform,false);
            characterEffectsCanvas=host.GetComponent<Canvas>();characterEffectsCanvas.renderMode=RenderMode.ScreenSpaceOverlay;characterEffectsCanvas.sortingOrder=10;
            var scaler=host.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            CharacterLowHealth=host.AddComponent<LowHealthEffect>();CharacterLowHealth.Bind(health);
            // The exploration prefab may have an old canvas. One presenter owns the crosshair now.
            foreach(var image in FindObjectsByType<Image>(FindObjectsSortMode.None).Where(i=>i.name=="Crosshair"))image.enabled=false;
            var dot=new GameObject("Crosshair",typeof(RectTransform),typeof(Image));dot.transform.SetParent(host.transform,false);
            characterCrosshair=dot.GetComponent<Image>();characterCrosshair.color=new Color(1,1,1,.75f);characterCrosshair.raycastTarget=false;
            var rect=dot.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Vector2.one*.5f;rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(6,6);
        }
        void TickCharacterEffects()
        {
            if(characterEffectsCanvas==null)return;
            bool visible=Ready&&!Cinematic&&!ForestMenu.Visible&&Panel==0;
            characterEffectsCanvas.enabled=visible;CharacterLowHealth.enabled=visible;
            characterCrosshair.enabled=visible&&Chapter?.Binoculars!=true&&Bag?.ThrowableEquipped!=true&&Player.GetComponent<Map01Mosin>()?.Scoped!=true;
        }
    }
}
