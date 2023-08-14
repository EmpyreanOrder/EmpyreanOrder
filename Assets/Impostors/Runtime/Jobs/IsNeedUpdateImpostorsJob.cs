using Impostors.Structs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Impostors.Jobs
{
    [BurstCompile]
    public struct IsNeedUpdateImpostorsJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<SharedData> sharedDataArray;

        public NativeArray<InstanceData> impostors;
        public float3 cameraPosition;
        public float3 lightDirection;
        public float multiplier;
        public float textureSizeMultiplier;
        public float gameTime;

        public void Execute(int index)
        {
            InstanceData curInstanceData = impostors[index];
            SharedData sharedData = sharedDataArray[index];
            float3 imposterPosition = sharedData.data.position;
            float3 fromCamToBill = cameraPosition - imposterPosition;
            float nowDistance = math.length(fromCamToBill);
            float screenSize = sharedData.data.height / (nowDistance * multiplier);
            var lightDirectionCopy = lightDirection;
            var time = gameTime;
            float textureSizeMultiplier = this.textureSizeMultiplier;

            screenSize = math.clamp(screenSize, 0, 1);

            curInstanceData.nowScreenSize = screenSize;
            curInstanceData.nowDirection = fromCamToBill;
            curInstanceData.nowDistance = nowDistance;
            bool isForcedUpdateRequested = curInstanceData.requiredAction ==
                                           InstanceData.RequiredAction.ForcedUpdateImpostorTexture;
            curInstanceData.requiredAction = InstanceData.RequiredAction.NotSet;

            if (curInstanceData.HasImpostor == false)
            {
                if (curInstanceData.nowScreenSize <
                    sharedData.settings.screenRelativeTransitionHeight &&
                    curInstanceData.nowScreenSize >
                    sharedData.settings.screenRelativeTransitionHeightCull)
                    curInstanceData.requiredAction = InstanceData.RequiredAction.GoToImpostorMode;
                goto ExitLabel;
            }

            if (screenSize > sharedData.settings.screenRelativeTransitionHeight)
            {
                curInstanceData.requiredAction = InstanceData.RequiredAction.GoToNormalMode;
                goto ExitLabel;
            }

            if (screenSize < sharedData.settings.screenRelativeTransitionHeightCull)
            {
                curInstanceData.requiredAction = InstanceData.RequiredAction.Cull;
                goto ExitLabel;
            }

            if (isForcedUpdateRequested)
            {
                curInstanceData.requiredAction = InstanceData.RequiredAction.GoToImpostorMode;
                goto ExitLabel;
            }

            bool needUpdate = IsNeedUpdate();

            bool IsNeedUpdate()
            {
                // check if angle from last update bigger than maxAngleTreshold
                float angle = math.degrees(AngleInRad(curInstanceData.lastUpdate.cameraDirection, fromCamToBill));
                float lightAngle= 0  ;

                float AngleInRad(float3 vec1, float3 vec2)
                {
                    float some = (vec1.x * vec2.x + vec1.y * vec2.y + vec1.z * vec2.z) /
                                 math.sqrt(math.lengthsq(vec1) * math.lengthsq(vec2));
                    if (1 - some < 0.0001f)
                        return 0.0f;
                    some = math.acos(some);
                    some = math.abs(some);
                    return some;
                }

                if (sharedData.settings.isStatic == false)
                {
                    angle += math.degrees(AngleInRad(curInstanceData.lastUpdate.objectForwardDirection,
                        sharedData.data.forward));
                }

                // If light angle has changed
                if (sharedData.settings.useDeltaLightAngle == 1)
                {
                    lightAngle = AngleInRad(curInstanceData.lastUpdate.lightDirection, lightDirectionCopy) *
                                 Mathf.Rad2Deg;
                    curInstanceData.angleDifferenceSinceLastUpdate = angle + lightAngle;
                    if (lightAngle > sharedData.settings.deltaLightAngle)
                        return true;
                }
                
                curInstanceData.angleDifferenceSinceLastUpdate = angle + lightAngle;
                if (angle > sharedData.settings.deltaCameraAngle)
                    return true;

                if (sharedData.settings.useUpdateByTime == 1 &&
                    (time - curInstanceData.lastUpdate.time) > sharedData.settings.timeInterval)
                    return true;

                

                // if need to change resolution of imposter texture
                int resolution = (int) (screenSize * textureSizeMultiplier);
                resolution = math.ceilpow2(resolution);
                resolution = math.clamp(resolution, sharedData.settings.minTextureResolution,
                    sharedData.settings.maxTextureResolution);
                
                if (resolution > curInstanceData.lastUpdate.textureResolution)
                    return true;


                // if size on screen changed 
                float distance = math.abs(nowDistance - curInstanceData.lastUpdate.distance) /
                                 curInstanceData.lastUpdate.distance;
                if (distance > sharedData.settings.deltaDistance)
                    return true;

                return false;
            }

            curInstanceData.requiredAction = needUpdate
                ? InstanceData.RequiredAction.UpdateImpostorTexture
                : InstanceData.RequiredAction.None;

            ExitLabel:
            impostors[index] = curInstanceData;
        }
    }
}