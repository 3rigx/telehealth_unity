// https://stackoverflow.com/questions/34460587/unity-changing-only-certain-part-of-3d-models-color

using UnityEngine;
using UnityEngine.Assertions;

namespace Assets.Scripts.AvatarRenderer
{
    [DisallowMultipleComponent]
    public class FeetColorer : MonoBehaviour
    {
        private const int leftFoot = 50, leftToeBase = 51;
        private const int rightFoot = 46, rightToeBase = 47;

        public Color32 RegularColor = Color.white;

        public SkinnedMeshRenderer Smr;

        // Find bone index given bone transform
        private int GetBoneIndex(Transform bone)
        {
            Debug.Assert(Smr != null);
            Debug.Log(bone.name);
            var bones = Smr.bones;

            for (var i = 0; i < bones.Length; ++i)
            {
                Debug.Log("Bone " + i + " " + bones[i].name);
                if (bones[i] == bone) return i;
            }

            return -1;
        }

        private float GetBoneWeight(BoneWeight weight, int target)
        {
            float w = 0;
            if (weight.boneIndex0 == target && weight.weight0 > 0)
                w += weight.weight0;
            if (weight.boneIndex1 == target && weight.weight1 > 0)
                w += weight.weight1;
            if (weight.boneIndex2 == target && weight.weight2 > 0)
                w += weight.weight2;
            if (weight.boneIndex3 == target && weight.weight3 > 0)
                w += weight.weight3;
            return w;
        }

        // Change vertex colors highlighting given bone
        public void UpdateColor(Color32 leftHeelColor, Color32 leftToeColor, Color32 rightHeelColor,
            Color32 rightToeColor)
        {
            if (!Smr) return; // Skeleton Viewer not active
            var mesh = Smr.sharedMesh;
            var weights = mesh.boneWeights;
            var colors = new Color32[weights.Length];

            for (var i = 0; i < colors.Length; ++i)
            {
                float rfsum = GetBoneWeight(weights[i], rightFoot), rtsum = GetBoneWeight(weights[i], rightToeBase);
                float lfsum = GetBoneWeight(weights[i], leftFoot), ltsum = GetBoneWeight(weights[i], leftToeBase);
                Color32 rightColor, leftColor;
                rightColor = rfsum > rtsum ? rightHeelColor : rightToeColor;
                leftColor = lfsum > ltsum ? leftHeelColor : leftToeColor;
                float rsum = rfsum + rtsum, lsum = lfsum + ltsum;
                colors[i] = rsum > lsum
                    ? Color32.Lerp(RegularColor, rightColor, rsum)
                    : Color32.Lerp(RegularColor, leftColor, lsum);
            }

            mesh.colors32 = colors;
        }


        private void Start()
        {
            // Find smr if not given
            if (Smr == null) Smr = GetComponent<SkinnedMeshRenderer>();
            Assert.IsNotNull(Smr, "SkinnedMeshRenderer not found");
            // SkinnedMeshRenderer has only shared mesh. We should not modify it.
            // So we make a copy on startup, and work with it.
            Smr.sharedMesh = Instantiate(Smr.sharedMesh);
        }
    }
}