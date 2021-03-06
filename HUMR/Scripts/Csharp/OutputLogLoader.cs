﻿
/*******
 * OutputLogLoader.cs
 * 
 * メインの処理を行う。ログ出力時と同一のアバターをHierarchy上に置き、これをアタッチして使用することを想定している
 * PackageManagerからFBXExportorをインストールしておく必要あり
 * 
 * フォルダを構成して、OutputLog_xx_xx_xxからアニメーションを作成
 * そのアニメーションをアバターのアニメーターに入れてFBXとして出力
 * FBXをHumanoidにすることでHumanoidAnimationを得られるようにしている
 * 
 * *****/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine.EventSystems;

namespace HUMR
{
#if UNITY_EDITOR
    public interface OutputLogLoaderinterface : IEventSystemHandler
    {
        void LoadLogToExportAnim();
    }

    [RequireComponent(typeof(Animator))]
    public class OutputLogLoader : MonoBehaviour, OutputLogLoaderinterface
    {
        Animator animator;
        UnityEditor.Animations.AnimatorController controller;
        string[] files;
        [HideInInspector]
        public string OutputLogPath = "";
        [HideInInspector]
        public int index = 0;

        static int nHeaderStrNum = 19;//timestamp example/*2021.01.03 20:57:35*/
        static string strKeyWord = " Log        -  HUMR:";
        [TooltipAttribute("GenericAnimationを出力する場合はチェックを入れてください")]
        public bool ExportGenericAnimation = false;
        [TooltipAttribute("モーションを出力したいユーザーの名前を書いてください")]
        public string DisplayName = "";

        public void LoadLogToExportAnim()
        {
            if (DisplayName == "")
            {
                Debug.LogWarning("DisplayName is null");
                return;
            }
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
            string humrPath = @"Assets/HUMR";
            CreateDirectoryIfNotExist(humrPath);

            ControllerSetUp(humrPath);

            AnimationClip clip = new AnimationClip();

            string[] files = Directory.GetFiles(OutputLogPath, "*.txt");

            string[] strOutputLogLines = File.ReadAllLines(files[index]);
            int nTargetCounter = 0;
            for (int j = 0; j < strOutputLogLines.Length; j++)
            {
                //対象のログの行を抽出
                if (strOutputLogLines[j].Contains(strKeyWord + DisplayName))
                {
                    if (strOutputLogLines[j].Length > nHeaderStrNum + (strKeyWord + DisplayName).Length)
                    {
                        nTargetCounter++;//目的の行が何行あるか。
                    }
                    else
                    {
                        Debug.LogWarning("Length is not correct");
                    }
                }
            }

            // Keyframeの生成
            if (nTargetCounter == 0)
            {
                Debug.Log("Not exist Motion Data");
                return;
            }

            Keyframe[][] Keyframes = new Keyframe[4 * (HumanTrait.BoneName.Length + 1/*time + hip position*/) - 1/*time*/][];//[要素数]
            for (int j = 0; j < Keyframes.Length; j++)
            {
                Keyframes[j] = new Keyframe[nTargetCounter];//[行数]
            }

            //Keyframeにログの値を入れていく
            {
                string[] strDisplayNameOutputLogLines = new string[nTargetCounter];//目的の行の配列
                int nTargetLineCounter = 0;
                for (int j = 0; j < strOutputLogLines.Length; j++)
                {
                    //対象のログの行を抽出
                    if (strOutputLogLines[j].Contains(strKeyWord + DisplayName))
                    {
                        if (strOutputLogLines[j].Length > nHeaderStrNum + (strKeyWord + DisplayName).Length)
                        {
                            strDisplayNameOutputLogLines[nTargetLineCounter] = strOutputLogLines[j].Substring(nHeaderStrNum + (strKeyWord + DisplayName).Length);//時間,position,rotation,rotation,…
                        }
                        else
                        {
                            Debug.LogWarning("Log Length is not correct");
                        }
                        //Debug.Log(DisplayNameOutputLogLines[nTargetLineCounter]);
                        string[] strSplitedOutPutLog = strDisplayNameOutputLogLines[nTargetLineCounter].Split(',');
                        if (strSplitedOutPutLog.Length == 4 * (HumanTrait.BoneName.Length + 1/*time + hip position*/))
                        {
                            float key_time = float.Parse(strSplitedOutPutLog[0]);
                            Vector3 rootScale = animator.transform.localScale;
                            Vector3 hippos = new Vector3(float.Parse(strSplitedOutPutLog[1]), float.Parse(strSplitedOutPutLog[2]), float.Parse(strSplitedOutPutLog[3]));
                            transform.rotation = Quaternion.identity;//Avatarがrotation(0,0,0)でない可能性があるため
                            hippos = Quaternion.Inverse(animator.GetBoneTransform((HumanBodyBones)0).parent.localRotation) * hippos;//armatureがrotation(0,0,0)でない可能性があるため
                            hippos = new Vector3(hippos.x / rootScale.x, hippos.y / rootScale.y, hippos.z / rootScale.z); //いる？
                            Keyframes[0][nTargetLineCounter] = new Keyframe(key_time, hippos.x);
                            Keyframes[1][nTargetLineCounter] = new Keyframe(key_time, hippos.y);
                            Keyframes[2][nTargetLineCounter] = new Keyframe(key_time, hippos.z);
                            Quaternion[] boneWorldRotation = new Quaternion[HumanTrait.BoneName.Length];
                            for (int k = 0; k < HumanTrait.BoneName.Length; k++)
                            {
                                boneWorldRotation[k] = new Quaternion(float.Parse(strSplitedOutPutLog[k * 4 + 4]), float.Parse(strSplitedOutPutLog[k * 4 + 5]), float.Parse(strSplitedOutPutLog[k * 4 + 6]), float.Parse(strSplitedOutPutLog[k * 4 + 7]));
                            }
                            for (int k = 0; k < HumanTrait.BoneName.Length; k++)
                            {

                                if (animator.GetBoneTransform((HumanBodyBones)k) == null)
                                {
                                    continue;
                                }
                                animator.GetBoneTransform((HumanBodyBones)k).rotation = boneWorldRotation[k];
                            }

                            for (int k = 0; k < HumanTrait.BoneName.Length; k++)
                            {
                                if (animator.GetBoneTransform((HumanBodyBones)k) == null)
                                {
                                    continue;
                                }
                                Quaternion localrot = animator.GetBoneTransform((HumanBodyBones)k).localRotation;
                                if (k==0 && localrot.w < 0)//localrot.w=1,-1遷移時のノイズを抑える目的でlocalrot.w>＝0しか許容しない。hipのみ。handとfootもやった方がいいかも？
                                {
                                    localrot = new Quaternion(-localrot.x, -localrot.y, -localrot.z, -localrot.w);
                                }
                                Keyframes[k * 4 + 3][nTargetLineCounter] = new Keyframe(key_time, localrot.x);
                                Keyframes[k * 4 + 4][nTargetLineCounter] = new Keyframe(key_time, localrot.y);
                                Keyframes[k * 4 + 5][nTargetLineCounter] = new Keyframe(key_time, localrot.z);
                                Keyframes[k * 4 + 6][nTargetLineCounter] = new Keyframe(key_time, localrot.w);
                            }
                        }
                        else
                        {
                            Debug.Log(strSplitedOutPutLog.Length);//228
                            Debug.LogAssertion("Key value length is not correct");
                        }
                        nTargetLineCounter++;
                    }
                }
            }

            //AnimationClipにAnimationCurveを設定
            {
                // AnimationCurveの生成
                AnimationCurve[] AnimCurves = new AnimationCurve[Keyframes.Length];

                for (int l = 0; l < AnimCurves.Length; l++)//[行数-1]
                {
                    AnimCurves[l] = new AnimationCurve(Keyframes[l]);
                }
                // AnimationCurveの追加
                clip.SetCurve(GetHierarchyPath(animator.GetBoneTransform((HumanBodyBones)0)), typeof(Transform), "localPosition.x", AnimCurves[0]);
                clip.SetCurve(GetHierarchyPath(animator.GetBoneTransform((HumanBodyBones)0)), typeof(Transform), "localPosition.y", AnimCurves[1]);
                clip.SetCurve(GetHierarchyPath(animator.GetBoneTransform((HumanBodyBones)0)), typeof(Transform), "localPosition.z", AnimCurves[2]);
                for (int m = 0; m < (AnimCurves.Length - 3) / 4; m++)//[骨数]
                {
                    if (animator.GetBoneTransform((HumanBodyBones)m) == null)
                    {
                        continue;
                    }
                    clip.SetCurve(GetHierarchyPath(animator.GetBoneTransform((HumanBodyBones)m)),
                        typeof(Transform), "localRotation.x", AnimCurves[m * 4 + 3]);
                    clip.SetCurve(GetHierarchyPath(animator.GetBoneTransform((HumanBodyBones)m)),
                        typeof(Transform), "localRotation.y", AnimCurves[m * 4 + 4]);
                    clip.SetCurve(GetHierarchyPath(animator.GetBoneTransform((HumanBodyBones)m)),
                        typeof(Transform), "localRotation.z", AnimCurves[m * 4 + 5]);
                    clip.SetCurve(GetHierarchyPath(animator.GetBoneTransform((HumanBodyBones)m)),
                        typeof(Transform), "localRotation.w", AnimCurves[m * 4 + 6]);
                }
            }

            //GenericAnimation出力
            {
                string animFolderPath = humrPath + @"/GenericAnimations";
                CreateDirectoryIfNotExist(animFolderPath);
                string displaynameFolderPath = animFolderPath + "/" + DisplayName;
                CreateDirectoryIfNotExist(displaynameFolderPath);

                string animationName = files[index].Substring(files[index].Length - 13).Remove(9);
                string animPath = displaynameFolderPath + "/" + animationName + ".anim";
                Debug.Log(animPath);

                if (ExportGenericAnimation)
                {
                    if (File.Exists(animPath))
                    {
                        AssetDatabase.DeleteAsset(animPath);
                        Debug.LogWarning("Same Name Generic Animation is existing. Overwritten!!");
                        ControllerSetUp(humrPath);
                    }
                    AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(animPath));
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            //アニメーションをアバターのアニメーターに入れてFBXとして出力
            {
                controller.layers[0].stateMachine.AddState(clip.name).motion = clip;
                animator.runtimeAnimatorController = controller;
                string exportFolderPath = humrPath + @"/FBXs";
                CreateDirectoryIfNotExist(exportFolderPath);
                string displaynameFBXFolderPath = exportFolderPath + "/" + DisplayName;
                CreateDirectoryIfNotExist(displaynameFBXFolderPath);
                
                UnityEditor.Formats.Fbx.Exporter.ModelExporter.ExportObject(displaynameFBXFolderPath + "/" + clip.name, this.gameObject);
            }
        }
        
        void ControllerSetUp(string humrPath)
        {
            string tmpAniConPath = humrPath + @"/AnimationController";
            if (controller == null)
            {
                CreateDirectoryIfNotExist(tmpAniConPath);
                controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(tmpAniConPath + "/TmpAniCon.controller");
            }
            else if (AssetDatabase.GetAssetPath(controller) == tmpAniConPath + "/TmpAniCon")
            {
                foreach (var layer in controller.layers)
                {
                    foreach (var state in layer.stateMachine.states)
                    {
                        layer.stateMachine.RemoveState(state.state);
                    }
                }
            }
            else
            {
                foreach (var layer in controller.layers)
                {
                    foreach (var state in layer.stateMachine.states)
                    {
                        if (state.state.motion == null)
                        {
                            layer.stateMachine.RemoveState(state.state);
                        }
                    }
                }
            }
        }

        void CreateDirectoryIfNotExist(string path)
        {
            //存在するかどうか判定しなくても良いみたいだが気持ち悪いので
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        string GetHierarchyPath(Transform self)
        {
            string path = self.gameObject.name;
            Transform parent = self.parent;
            while (parent.parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

    }
#endif
}