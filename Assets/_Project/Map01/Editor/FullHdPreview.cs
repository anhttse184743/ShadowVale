using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.EditorTools
{
    public static class FullHdPreview
    {
        [MenuItem("ShadowVale/Preview/Full HD 1920x1080")]
        public static void Apply()
        {
            PlayerSettings.defaultScreenWidth=1920;
            PlayerSettings.defaultScreenHeight=1080;
            PlayerSettings.defaultIsNativeResolution=false;
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance").GetValue(null);
            var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
            var methods=group.GetType();
            int count=(int)methods.GetMethod("GetTotalCount").Invoke(group,null),selected=-1;
            for(int i=0;i<count;i++){
                var entry=methods.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
                if((int)entry.GetType().GetProperty("width").GetValue(entry)==1920 && (int)entry.GetType().GetProperty("height").GetValue(entry)==1080){selected=i;break;}
            }
            if(selected<0){
                var sizeType=assembly.GetType("UnityEditor.GameViewSize");
                var modeType=assembly.GetType("UnityEditor.GameViewSizeType");
                var size=Activator.CreateInstance(sizeType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{Enum.Parse(modeType,"FixedResolution"),(object)1920,1080,"Full HD"},null);
                methods.GetMethod("AddCustomSize").Invoke(group,new[]{size});selected=count;
            }
            var viewType=assembly.GetType("UnityEditor.GameView");
            var view=EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,selected);
            view.Repaint();AssetDatabase.SaveAssets();
            Debug.Log("ShadowVale preview: Full HD 1920x1080; native resolution disabled.");
        }
    }
}