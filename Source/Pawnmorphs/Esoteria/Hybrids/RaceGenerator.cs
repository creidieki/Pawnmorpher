﻿// RaceGenerator.cs modified by Iron Wolf for Pawnmorph on 08/02/2019 7:12 PM
// last updated 08/02/2019  7:12 PM


//uncomment to test custom draw sizes 
//#define TEST_BODY_SIZE 



using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using AlienRace;
using JetBrains.Annotations;
using Pawnmorph.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace Pawnmorph.Hybrids
{
    /// <summary> Static class responsible for generating the implicit races.</summary>
    public static class RaceGenerator
    {
        private const float HEALTH_SCALE_LERP_VALUE = 0.4f;
        private const float HUNGER_LERP_VALUE = 0.3f;
        private static List<ThingDef_AlienRace> _lst;

        [NotNull]
        private static readonly Dictionary<ThingDef, MorphDef> _raceLookupTable = new Dictionary<ThingDef, MorphDef>();

        /// <summary>an enumerable collection of all implicit races generated by the MorphDefs</summary>
        /// includes unused implicit races generated if the MorphDef has an explicit hybrid race 
        /// <value>The implicit races.</value>
        [NotNull]
        public static IEnumerable<ThingDef_AlienRace> ImplicitRaces => _lst ?? (_lst = GenerateAllImpliedRaces().ToList());

        /// <summary> Try to find the morph def associated with the given race.</summary>
        /// <param name="race"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool TryGetMorphOfRace(ThingDef race, out MorphDef result)
        {
            return _raceLookupTable.TryGetValue(race, out result);
        }

        /// <summary> Gets the morph Def associated with this race, if any.</summary>
        /// <param name="race"></param>
        /// <returns></returns>
        [CanBeNull]
        public static MorphDef GetMorphOfRace(this ThingDef race)
        {
            return _raceLookupTable.TryGetValue(race);
        }

        private static RaceProperties GenerateHybridProperties([NotNull] RaceProperties human, [NotNull] RaceProperties animal)
        {
            (float hSize, float hHRate) = GetFoodStats(human, animal); 


            return new RaceProperties
            {
                thinkTreeMain = human.thinkTreeMain, //most of these are just guesses, have to figure out what's safe to change and what isn't 
                thinkTreeConstant = human.thinkTreeConstant,
                intelligence = human.intelligence,
                makesFootprints = true,
                lifeExpectancy = human.lifeExpectancy,
                leatherDef = animal.leatherDef,
                nameCategory = human.nameCategory,
                body = human.body,
                baseBodySize = hSize,
                baseHealthScale = Mathf.Lerp(human.baseHealthScale, animal.baseHealthScale, HEALTH_SCALE_LERP_VALUE),
                baseHungerRate = hHRate,
                foodType = GenerateFoodFlags(animal.foodType),
                gestationPeriodDays = human.gestationPeriodDays,
                meatColor = animal.meatColor,
                meatMarketValue = animal.meatMarketValue,
                manhunterOnDamageChance = animal.manhunterOnDamageChance,
                manhunterOnTameFailChance = animal.manhunterOnTameFailChance,
                litterSizeCurve = human.litterSizeCurve,
                lifeStageAges = MakeLifeStages(human.lifeStageAges, animal.lifeStageAges),
                soundMeleeHitPawn = animal.soundMeleeHitPawn,
                soundMeleeHitBuilding = animal.soundMeleeHitBuilding,
                soundMeleeMiss = animal.soundMeleeMiss,
                specialShadowData = human.specialShadowData,
                soundCallIntervalRange = animal.soundCallIntervalRange,
                ageGenerationCurve = human.ageGenerationCurve,
                hediffGiverSets = human.hediffGiverSets.ToList(),
                meatDef = animal.meatDef,
                meatLabel = animal.meatLabel,
                useMeatFrom = animal.useMeatFrom,
                deathActionWorkerClass = animal.deathActionWorkerClass, // Boommorphs should explode.
                corpseDef = human.corpseDef,
                packAnimal = animal.packAnimal
            };
        }

        static (float bodySize, float hungerSize) GetFoodStats([NotNull] RaceProperties human, [NotNull] RaceProperties animal)
        {
            //'gamma' is a ratio describing how long it takes an animal to become hungry 
            //larger values mean the animal needs to eat less often 
            float gammaH = human.baseBodySize / human.baseHungerRate;
            float gammaA = animal.baseBodySize / animal.baseHungerRate;

            float f = Mathf.Pow(Math.Abs((gammaA - gammaH) / gammaH), 0.5f);
            //scale things back a bit if the animal has very different hunger characteristics then humans  
            float a = 1 / (1f + f); 

            float hGamma = Mathf.Lerp(gammaA, gammaH, a);
            //body size is just an average of animal and human 
            float hBSize = Mathf.Lerp(animal.baseBodySize, human.baseBodySize, a);
            float hHRate = hBSize / hGamma; //calculate the hunger rate the hybrid should have to have an average gamma value between the animal and human 

            return (hBSize, hHRate); 
        } 

        private static List<LifeStageAge> MakeLifeStages(List<LifeStageAge> human, List<LifeStageAge> animal)
        {
            List<LifeStageAge> ls = new List<LifeStageAge>();

            float convert = ((float) animal.Count) / human.Count;  
            for (int i = 0; i < human.Count; i++)
            {
                int j = (int) (convert * i);

                var hStage = human[i];
                var aStage = animal[j];

                var newStage = new LifeStageAge()
                {
                    minAge = hStage.minAge,
                    def = hStage.def,
                    soundAngry = aStage.soundAngry,
                    soundCall = aStage.soundCall,
                    soundDeath = aStage.soundDeath,
                    soundWounded = aStage.soundWounded
                };
                ls.Add(newStage); 

            }

            return ls; 
        }

        private static float GetBodySize(RaceProperties animal, RaceProperties human)
        {
            var f = Mathf.Lerp(human.baseBodySize, animal.baseBodySize, 0.5f);
            return Mathf.Max(f, human.baseBodySize * 0.7f);
        }

        private static float GetHungerRate(RaceProperties animal, RaceProperties human)
        {
            
            var f = Mathf.Lerp(human.baseHungerRate, animal.baseHungerRate, 0.7f);
            return f;
        }

        [NotNull]
        private static IEnumerable<ThingDef_AlienRace> GenerateAllImpliedRaces()
        {
            IEnumerable<MorphDef> morphs = DefDatabase<MorphDef>.AllDefs;
            ThingDef_AlienRace human;

            try
            {
                human = (ThingDef_AlienRace) ThingDef.Named("Human");
            }
            catch (InvalidCastException e)
            {
                throw new
                    ModInitializationException($"could not convert human ThingDef to {nameof(ThingDef_AlienRace)}! is HAF up to date?",e);
            }


            var builder = new StringBuilder();
            // ReSharper disable once PossibleNullReferenceException
            foreach (MorphDef morphDef in morphs)
            {
                builder.AppendLine($"generating implied race for {morphDef.defName}");
                ThingDef_AlienRace race = GenerateImplicitRace(human, morphDef);
                _raceLookupTable[race] = morphDef;

                if (morphDef.ExplicitHybridRace == null) //still generate the race so we don't break saves, but don't set them 
                {
                    morphDef.hybridRaceDef = race;
                }
                else
                {
                    _raceLookupTable[morphDef.ExplicitHybridRace] = morphDef;
                    builder.AppendLine($"\t\t{morphDef.defName} has explicit hybrid race {morphDef.ExplicitHybridRace.defName}, {race.defName} will not be used but still generated");
                }
                


                CreateImplicitMeshes(builder, race);

                yield return race;
            }

            Log.Message(builder.ToString());
        }

        private static void CreateImplicitMeshes(StringBuilder builder, ThingDef_AlienRace race)
        {
            try
            {
                //generate any meshes the implied race might need 
                if (race.alienRace?.graphicPaths != null)
                {
                    builder.AppendLine($"Generating mesh pools for {race.defName}");
                    race.alienRace.generalSettings?.alienPartGenerator?.GenerateMeshsAndMeshPools();
                }
            }
            catch (Exception e)
            {
                throw new ModInitializationException($"while updating graphics for {race.defName}, caught {e.GetType().Name}",e);

            }
        }

        /// <summary>
        /// Generate general settings for the hybrid race given the human settings and morph def.
        /// </summary>
        /// <param name="human">The human.</param>
        /// <param name="morph">The morph.</param>
        /// <param name="impliedRace">The implied race.</param>
        /// <returns></returns>
        private static GeneralSettings GenerateHybridGeneralSettings(GeneralSettings human, MorphDef morph, ThingDef_AlienRace impliedRace)
        {
            var traitSettings = morph.raceSettings.traitSettings;
            return new GeneralSettings
            {
                alienPartGenerator = GenerateHybridGenerator(human.alienPartGenerator, morph, impliedRace),
                humanRecipeImport = true,
                forcedRaceTraitEntries = traitSettings?.forcedTraits
                // Black list is not currently supported, Rimworld doesn't like it when you remove traits.
            };
        }

        private static AlienPartGenerator GenerateHybridGenerator(AlienPartGenerator human, MorphDef morph, ThingDef_AlienRace impliedRace)
        {
            AlienPartGenerator gen = new AlienPartGenerator
            {
                alienbodytypes = human.alienbodytypes.MakeSafe().ToList(),
                aliencrowntypes = human.aliencrowntypes.MakeSafe().ToList(),
                bodyAddons = GenerateBodyAddons(human.bodyAddons, morph),
                alienProps = impliedRace
            };

            return gen;
        }

        static VTuple<Vector2, Vector2>? GetDebugBodySizes(MorphDef morph)
        {
            
            var pkDef = DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(pk => pk.race == morph.race);//get the first pawnkindDef that uses the morph's race 

            if (pkDef?.lifeStages == null || pkDef.lifeStages.Count == 0) //if there are no pawnkinds to choose from just return null 
            {
                return null;
            }

            var lastStage = pkDef.lifeStages.Last();

            float cSize = Mathf.Lerp(1, lastStage.bodyGraphicData.drawSize.x,0.5f); //take the average of the animals draw size and a humans, which is a default of 1 
            return new VTuple<Vector2, Vector2>(cSize * Vector2.one, cSize * Vector2.one); 
        }


        private static List<AlienPartGenerator.BodyAddon> GenerateBodyAddons(List<AlienPartGenerator.BodyAddon> human, MorphDef morph)
        {
            List<AlienPartGenerator.BodyAddon> returnList = new List<AlienPartGenerator.BodyAddon>();

#if TEST_BODY_SIZE
            var tuple = GetDebugBodySizes(morph);
            Vector2? bodySize = tuple?.First, headSize = tuple?.Second;

            if (tuple != null)
            {
                Log.Message($"{morph.defName} draw sizes are: bodySize={tuple.Value.First}, headSize={tuple.Value.Second}");
            }
#else
            Vector2? bodySize = morph?.raceSettings?.graphicsSettings?.customDrawSize;
            Vector2? headSize = morph?.raceSettings?.graphicsSettings?.customHeadDrawSize;

#endif




            List<string> headParts = new List<string>();
            headParts.Add("Jaw");
            headParts.Add("Ear");
            headParts.Add("left ear");
            headParts.Add("right ear");
            headParts.Add("Skull");

            List<string> bodyParts = new List<string>();
            bodyParts.Add("Arm");
            bodyParts.Add("Tail");
            bodyParts.Add("Waist");

            foreach (AlienPartGenerator.BodyAddon addon in human)
            {
                AlienPartGenerator.BodyAddon temp = new AlienPartGenerator.BodyAddon()
                {
                    path = addon.path,
                    bodyPart = addon.bodyPart,
                    offsets = GenerateBodyAddonOffsets(addon.offsets, morph),
                    linkVariantIndexWithPrevious = addon.linkVariantIndexWithPrevious,
                    angle = addon.angle,
                    inFrontOfBody = addon.inFrontOfBody,
                    layerOffset = addon.layerOffset,
                    layerInvert = addon.layerInvert,
                    drawnOnGround = addon.drawnOnGround,
                    drawnInBed = addon.drawnInBed,
                    drawForMale = addon.drawForMale,
                    drawForFemale = addon.drawForFemale,
                    drawSize = addon.drawSize,
                    variantCount = addon.variantCount,
                    hediffGraphics = addon.hediffGraphics,
                    backstoryGraphics = addon.backstoryGraphics,
                    hiddenUnderApparelFor = addon.hiddenUnderApparelFor,
                    hiddenUnderApparelTag = addon.hiddenUnderApparelTag,
                    backstoryRequirement = addon.backstoryRequirement
                };

                if (headParts.Contains(temp.bodyPart))
                {
                    if (headSize != null)
                        temp.drawSize = headSize.GetValueOrDefault();
                    if (bodySize != null)
                    {
                        if (temp?.offsets?.south?.bodyTypes != null)
                            foreach (AlienPartGenerator.BodyTypeOffset bodyType in temp.offsets.south.bodyTypes)
                                bodyType.offset.y += 0.34f * (bodySize.GetValueOrDefault().y - 1f);
                        if (temp?.offsets?.north?.bodyTypes != null)
                            foreach (AlienPartGenerator.BodyTypeOffset bodyType in temp.offsets.north.bodyTypes)
                                bodyType.offset.y += 0.34f * (bodySize.GetValueOrDefault().y - 1f);
                        if (temp?.offsets?.east?.bodyTypes != null)
                            foreach (AlienPartGenerator.BodyTypeOffset bodyType in temp.offsets.east.bodyTypes)
                                bodyType.offset.y += 0.34f * (bodySize.GetValueOrDefault().y - 1f);
                        if (temp?.offsets?.west?.bodyTypes != null)
                            foreach (AlienPartGenerator.BodyTypeOffset bodyType in temp.offsets.west.bodyTypes)
                                bodyType.offset.y += 0.34f * (bodySize.GetValueOrDefault().y - 1f);
                    }
                }

                if (bodySize != null && bodyParts.Contains(temp.bodyPart))
                {
                    temp.drawSize = bodySize.GetValueOrDefault();
                }

                returnList.Add(temp);
            }

            return returnList;
        }

        private static AlienPartGenerator.BodyAddonOffsets GenerateBodyAddonOffsets(AlienPartGenerator.BodyAddonOffsets human, MorphDef morph)
        {
            AlienPartGenerator.BodyAddonOffsets returnValue = new AlienPartGenerator.BodyAddonOffsets();
            if (human.south != null)
                returnValue.south = GenerateRotationOffsets(human.south, morph);
            if (human.north != null)
                returnValue.north = GenerateRotationOffsets(human.north, morph);
            if (human.east != null)
                returnValue.east = GenerateRotationOffsets(human.east, morph);
            if (human.west != null)
                returnValue.west = GenerateRotationOffsets(human.west, morph);
            return returnValue;
        }

        private static AlienPartGenerator.RotationOffset GenerateRotationOffsets(AlienPartGenerator.RotationOffset human, MorphDef morph)
        {
            AlienPartGenerator.RotationOffset returnValue = new AlienPartGenerator.RotationOffset()
            {
                portraitBodyTypes = human.portraitBodyTypes,
                portraitCrownTypes = human.portraitCrownTypes,
                crownTypes = human.crownTypes
            };

            if (human.bodyTypes != null)
                returnValue.bodyTypes = GenerateBodyTypeOffsets(human.bodyTypes, morph);

            return returnValue;
        }

        private static List<AlienPartGenerator.BodyTypeOffset> GenerateBodyTypeOffsets(List<AlienPartGenerator.BodyTypeOffset> human, MorphDef morph)
        {
            List<AlienPartGenerator.BodyTypeOffset> returnList = new List<AlienPartGenerator.BodyTypeOffset>();
            foreach (AlienPartGenerator.BodyTypeOffset bodyTypeOffset in human)
            {
                AlienPartGenerator.BodyTypeOffset temp = new AlienPartGenerator.BodyTypeOffset()
                {
                    offset = bodyTypeOffset.offset,
                    bodyType = bodyTypeOffset.bodyType
                };
                returnList.Add(temp);
            }

            return returnList;
        }

        /// <summary> Generate the alien race restriction setting from the human default and the given morph.</summary>
        /// <param name="human"></param>
        /// <param name="morph"></param>
        /// <returns></returns>
        private static RaceRestrictionSettings GenerateHybridRestrictionSettings(RaceRestrictionSettings human, MorphDef morph)
        {
            return new RaceRestrictionSettings(); //TODO restriction settings like apparel and stuff  
        }

        private static ThingDef_AlienRace.AlienSettings GenerateHybridAlienSettings(ThingDef_AlienRace.AlienSettings human, MorphDef morph, ThingDef_AlienRace impliedRace)
        {
            return new ThingDef_AlienRace.AlienSettings
            {
                generalSettings = GenerateHybridGeneralSettings(human.generalSettings, morph, impliedRace),
                graphicPaths = GenerateGraphicPaths(human.graphicPaths, morph),
                hairSettings = human.hairSettings,
                raceRestriction = GenerateHybridRestrictionSettings(human.raceRestriction, morph),
                relationSettings = human.relationSettings,
                thoughtSettings = morph.raceSettings.GenerateThoughtSettings(human.thoughtSettings, morph)
            };
        }

        private static List<GraphicPaths> GenerateGraphicPaths(List<GraphicPaths> humanGraphicPaths, MorphDef morph)
        {
            GraphicPaths temp = new GraphicPaths();
            Vector2? customSize = morph?.raceSettings?.graphicsSettings?.customDrawSize;
            temp.customDrawSize = customSize ?? temp.customDrawSize;
            temp.customPortraitDrawSize = customSize ?? temp.customPortraitDrawSize;
            Vector2? customHeadSize = morph?.raceSettings?.graphicsSettings?.customHeadDrawSize;
            temp.customHeadDrawSize = customHeadSize ?? temp.customHeadDrawSize;
            temp.customPortraitHeadDrawSize = customHeadSize ?? temp.customPortraitHeadDrawSize;
            temp.headOffset = customSize != null ? new Vector2(0f, 0.34f * (morph.raceSettings.graphicsSettings.customDrawSize.GetValueOrDefault().y - 1)) : temp.headOffset;
            temp.headOffsetDirectional = humanGraphicPaths.First().headOffsetDirectional; 
            List<GraphicPaths> returnList = new List<GraphicPaths>();
            returnList.Add(temp);
            return returnList;
        }

        static List<StatModifier> GenerateHybridStatModifiers(List<StatModifier> humanModifiers, List<StatModifier> animalModifiers, List<StatModifier> statMods)
        {
            humanModifiers = humanModifiers ?? new List<StatModifier>();
            animalModifiers = animalModifiers ?? new List<StatModifier>();

            Dictionary<StatDef, float> valDict = new Dictionary<StatDef, float>();
            foreach (StatModifier humanModifier in humanModifiers)
            {
                valDict[humanModifier.stat] = humanModifier.value;
            }

            //just average them for now
            foreach (StatModifier animalModifier in animalModifiers)
            {
                float val;
                if (valDict.TryGetValue(animalModifier.stat, out val))
                {
                    val = Mathf.Lerp(val, animalModifier.value, 0.5f); //average the 2 
                }
                else val = animalModifier.value;

                valDict[animalModifier.stat] = val;
            }

            //now handle any statMods if they exist 
            if (statMods != null)
                foreach (StatModifier statModifier in statMods)
                {
                    float v = valDict.TryGetValue(statModifier.stat) + statModifier.value;
                    valDict[statModifier.stat] = v;
                }

            List<StatModifier> outMods = new List<StatModifier>();
            foreach (KeyValuePair<StatDef, float> keyValuePair in valDict)
            {
                outMods.Add(new StatModifier()
                {
                    stat = keyValuePair.Key,
                    value = keyValuePair.Value
                });
            }

            return outMods;
        }

        static FoodTypeFlags GenerateFoodFlags(FoodTypeFlags animalFlags)
        {
            animalFlags |= FoodTypeFlags.Meal; //make sure all hybrids can eat meals 
                                               //need to figure out a way to let them graze but not pick up plants 
            return animalFlags;
        }

        [NotNull]
        private static ThingDef_AlienRace GenerateImplicitRace([NotNull] ThingDef_AlienRace humanDef, [NotNull] MorphDef morph)
        {
            var impliedRace =  new ThingDef_AlienRace
            {
                defName = morph.defName + "Race_Implied", //most of these are guesses, should figure out what's safe to change and what isn't 
                label = morph.label,
                race = GenerateHybridProperties(humanDef.race, morph.race.race),
                thingCategories = humanDef.thingCategories,
                thingClass = humanDef.thingClass,
                category = humanDef.category,
                selectable = humanDef.selectable,
                tickerType = humanDef.tickerType,
                altitudeLayer = humanDef.altitudeLayer,
                useHitPoints = humanDef.useHitPoints,
                hasTooltip = humanDef.hasTooltip,
                soundImpactDefault = morph.race.soundImpactDefault,
                statBases = GenerateHybridStatModifiers(humanDef.statBases, morph.race.statBases, morph.raceSettings.statModifiers),
                inspectorTabs = humanDef.inspectorTabs.ToList(), //do we want any custom tabs? 
                comps = humanDef.comps.ToList(),
                drawGUIOverlay = humanDef.drawGUIOverlay,
                description = string.IsNullOrEmpty(morph.description) ? morph.race.description : morph.description,
                modContentPack = morph.modContentPack,
                inspectorTabsResolved = humanDef.inspectorTabsResolved?.ToList() ?? new List<InspectTabBase>(),
                recipes = new List<RecipeDef>(humanDef.recipes.MakeSafe()), //this is where the surgery operations live
                filth = morph.race.filth,
                filthLeaving = morph.race.filthLeaving,
                soundDrop = morph.race.soundDrop,
                soundInteract = morph.race.soundInteract,
                soundPickup = morph.race.soundPickup,
                socialPropernessMatters = humanDef.socialPropernessMatters,
                stuffCategories = humanDef.stuffCategories?.ToList(),
                designationCategory = humanDef.designationCategory,
                tradeTags = humanDef.tradeTags?.ToList(),
                tradeability = humanDef.tradeability
            };
            impliedRace.tools = new List<Tool>(humanDef.tools.MakeSafe().Concat(morph.race.tools.MakeSafe()));
            var verbField = typeof(ThingDef).GetField("verbs", BindingFlags.NonPublic | BindingFlags.Instance); 
            var vLst = impliedRace.Verbs.MakeSafe().Concat(morph.race.Verbs.MakeSafe()).ToList();

            verbField.SetValue(impliedRace, vLst); 


            impliedRace.alienRace = GenerateHybridAlienSettings(humanDef.alienRace, morph, impliedRace); 

            return impliedRace; 
        }

        /// <summary>
        /// Determines whether this race is a morph hybrid race
        /// </summary>
        /// <param name="raceDef">The race definition.</param>
        /// <returns>
        ///   <c>true</c> if the race is a morph hybrid race; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">raceDef</exception>
        public static bool IsMorphRace([NotNull]ThingDef raceDef)
        {
            if (raceDef == null) throw new ArgumentNullException(nameof(raceDef));
            return _raceLookupTable.ContainsKey(raceDef);
        }
    }
}