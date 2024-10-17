using System;
using System.IO;
using System.Xml.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using Helpers;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using System.Text.RegularExpressions;
using static Olto_Cost.OltoPartyWageModel;
//using TaleWorlds.ObjectSystem;
//using TaleWorlds.Localization;

namespace Olto_Cost
{
    public class Olto_Cost_SubModule : MBSubModuleBase
    {
        public static bool wage_double;
        public static float cost_troop;
        public static float cost_ransom;
        public static float cost_merc;
        public static float cost_mounted;
        public static bool hire_discount;

        public static float cost_xp_up;

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (Olto_Cost_SubModule.wage_double == true) { gameStarterObject.AddModel((GameModel)new OltoPartyWageModel()); }
            gameStarterObject.AddModel((GameModel)new OltoRansomValueCalculationModel());
            gameStarterObject.AddModel((GameModel)new OltoPartyTroopUpgradeModel());
        }
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            this.LoadSettings();
        }
        private void LoadSettings()
        {
            Settings settings = new XmlSerializer(typeof(Settings)).Deserialize((Stream)File.OpenRead(Path.Combine(BasePath.Name, "Modules/Olto_Cost/settings.xml"))) as Settings;
            Olto_Cost_SubModule.wage_double = settings.wage_double;
            Olto_Cost_SubModule.cost_troop = settings.cost_troop;
            Olto_Cost_SubModule.cost_ransom = settings.cost_ransom;
            Olto_Cost_SubModule.cost_merc = settings.cost_merc;
            Olto_Cost_SubModule.cost_mounted = settings.cost_mounted;
            Olto_Cost_SubModule.hire_discount = settings.hire_discount;

            Olto_Cost_SubModule.cost_xp_up = settings.cost_xp_up;

        }
    }

    [Serializable]
    public class Settings
    {
        public bool wage_double;
        public float cost_troop;
        public float cost_ransom;
        public float cost_merc;
        public float cost_mounted;
        public bool hire_discount;

        public float cost_xp_up;
    }

    // Wage
    internal class OltoPartyWageModel : DefaultPartyWageModel
    {
        public override int GetCharacterWage(CharacterObject character)
        {
            //vanila
            //    0 => 1,
            //    1 => 2,
            //    2 => 3, // celling (roundup) x1.5
            //    3 => 5,
            //    4 => 8,
            //    5 => 12,
            //    6 => 17, // celling x1.5 = 18
            //    _ => 23, // celling x1.41177 = vanila, celling 18 x1.5 = 27
            //};
            //if (character.Occupation == Occupation.Mercenary) { num = (int)((float)num * 1.5f); }
            //return num;
            //mod
            int num = character.Tier switch
            {
                0 => 0,
                1 => 1,
                2 => 2,
                3 => 4,
                4 => 8,
                5 => 16,
                6 => 32,
                _ => 64,
            };
            // Mercenary x1.25 (instead x2); add other: Bandit (Gangster), CaravanGuard (from GetTroopRecruitmentCost)
            if (character.Occupation == Occupation.Mercenary || character.Occupation == Occupation.Bandit || character.Occupation == Occupation.CaravanGuard)
                num = (int)((float)num * Olto_Cost_SubModule.cost_merc);
            // Horse x1.5
            if (character.Equipment.Horse.Item != null)
                num = MathF.Round((float)num * Olto_Cost_SubModule.cost_mounted);

            return num;
        }


        public override int GetTroopRecruitmentCost(CharacterObject troop, Hero buyerHero, bool withoutItemCost = false)
        {
            // Hire Cost Normalize 12-25-50, not 10-20-50 //mod (12.5f); vanila (6.25f)
            //          int num = 10 * MathF.Round((float)troop.Level * MathF.Pow(troop.Level, 0.65f) * 0.2f);
            //          num = ((troop.Level <= 1) ? 10 : ((troop.Level <= 6) ? 20 : ((troop.Level <= 11) ? 50 : ((troop.Level <= 16) ? 100 : ((troop.Level <= 21) ? 200 : ((troop.Level <= 26) ? 400 : ((troop.Level <= 31) ? 600 : ((troop.Level > 36) ? 1500 : 1000))))))));
            int num = MathF.Round(Olto_Cost_SubModule.cost_troop * MathF.Pow(2, (troop.Level + 4) / 5)); // mod (12.5f); vanila (6.25f) = 10,20,50,100,200,400,800; vanila_old = 1, 40, 100, 190, 300, 430, 580
            if (troop.Equipment.Horse.Item != null) num = MathF.Round((float)num * Olto_Cost_SubModule.cost_mounted); // vanila: && !withoutItemCost // horse_t1 (+150) (for T2 +300%) or horse_t2 (+500)
                                                                                                                      //merc x1.125 (instead x2)
            bool flag = troop.Occupation == Occupation.Mercenary || troop.Occupation == Occupation.Bandit || troop.Occupation == Occupation.CaravanGuard; //Occupation.Gangster (useless) to Bandit
            if (flag) { num = MathF.Round((float)num * Olto_Cost_SubModule.cost_merc); }
            //
            if (buyerHero != null)
            {
                ExplainedNumber explainedNumber = new ExplainedNumber(1f);
                if (troop.Tier >= 2 && buyerHero.GetPerkValue(DefaultPerks.Throwing.HeadHunter))
                    explainedNumber.AddFactor(DefaultPerks.Throwing.HeadHunter.SecondaryBonus);

                if (troop.IsInfantry)
                {
                    if (buyerHero.GetPerkValue(DefaultPerks.OneHanded.ChinkInTheArmor))
                        explainedNumber.AddFactor(DefaultPerks.OneHanded.ChinkInTheArmor.SecondaryBonus);

                    if (buyerHero.GetPerkValue(DefaultPerks.TwoHanded.ShowOfStrength))
                        explainedNumber.AddFactor(DefaultPerks.TwoHanded.ShowOfStrength.SecondaryBonus);

                    if (buyerHero.GetPerkValue(DefaultPerks.Polearm.HardyFrontline))
                        explainedNumber.AddFactor(DefaultPerks.Polearm.HardyFrontline.SecondaryBonus);

                    if (buyerHero.Culture.HasFeat(DefaultCulturalFeats.SturgianRecruitUpgradeFeat))
                    {
                        // mod change 1 to 0.4f (sturguan hire discount 25% to 10% for foot melee troops, at Khuzait for mounted troop)
                        //                      explainedNumber.AddFactor(DefaultCulturalFeats.SturgianRecruitUpgradeFeat.EffectBonus, GameTexts.FindText("str_culture"));
                        if (Olto_Cost_SubModule.hire_discount == false) explainedNumber.AddFactor(DefaultCulturalFeats.SturgianRecruitUpgradeFeat.EffectBonus, GameTexts.FindText("str_culture"));
                        else { explainedNumber.AddFactor(DefaultCulturalFeats.SturgianRecruitUpgradeFeat.EffectBonus * 0.4f, GameTexts.FindText("str_culture")); }
                    }
                }
                else if (troop.IsRanged)
                {
                    if (buyerHero.GetPerkValue(DefaultPerks.Bow.RenownedArcher))
                        explainedNumber.AddFactor(DefaultPerks.Bow.RenownedArcher.SecondaryBonus);

                    if (buyerHero.GetPerkValue(DefaultPerks.Crossbow.Piercer))
                        explainedNumber.AddFactor(DefaultPerks.Crossbow.Piercer.SecondaryBonus);

                    // mod change 0 to 0.4f (sturguan hire discount 0% to 10% for foot ranged troops, at Khuzait for mounted troop)
                    if (Olto_Cost_SubModule.hire_discount == true && buyerHero.Culture.HasFeat(DefaultCulturalFeats.SturgianRecruitUpgradeFeat))
                        explainedNumber.AddFactor(DefaultCulturalFeats.SturgianRecruitUpgradeFeat.EffectBonus * 0.4f, GameTexts.FindText("str_culture"));
                }

                if (troop.IsMounted && buyerHero.Culture.HasFeat(DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat))
                    explainedNumber.AddFactor(DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat.EffectBonus, GameTexts.FindText("str_culture"));

                // mod change 0.01f to 0.0033f; discount 15% to 5% for steward 25
                if (buyerHero.IsPartyLeader && buyerHero.GetPerkValue(DefaultPerks.Steward.Frugal))
                    explainedNumber.AddFactor(DefaultPerks.Steward.Frugal.SecondaryBonus * 0.33f);

                if (flag)
                {
                    if (buyerHero.GetPerkValue(DefaultPerks.Trade.SwordForBarter))
                        explainedNumber.AddFactor(DefaultPerks.Trade.SwordForBarter.PrimaryBonus);

                    if (buyerHero.GetPerkValue(DefaultPerks.Charm.SlickNegotiator))
                        explainedNumber.AddFactor(DefaultPerks.Charm.SlickNegotiator.PrimaryBonus);
                }

                num = MathF.Max(1, MathF.Round((float)num * explainedNumber.ResultNumber));
            }
            return num;
        }


        // RansomValue 0.8f //0.25f // Tier1 = 5/20 or 40/50
        internal class OltoRansomValueCalculationModel : DefaultRansomValueCalculationModel
        {
            public override int PrisonerRansomValue(CharacterObject prisoner, Hero sellerHero = null)
            {
                int troopRecruitmentCost = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(prisoner, null);
                float num = 0f;
                float num2 = 0f;
                float num3 = 1f;
                if (prisoner.HeroObject?.Clan != null)
                {
                    num = (float)((prisoner.HeroObject.Clan.Tier + 2) * 200) * ((prisoner.HeroObject.Clan.Leader == prisoner.HeroObject) ? 2f : 1f);
                    num2 = MathF.Sqrt(MathF.Max(0, prisoner.HeroObject.Gold)) * 6f;
                    if (prisoner.HeroObject.Clan.Kingdom != null)
                    {
                        int count = prisoner.HeroObject.Clan.Kingdom.Fiefs.Count;
                        num3 = ((!prisoner.HeroObject.MapFaction.IsKingdomFaction) ? 1f : ((count < 8) ? (((float)count + 1f) / 9f) : (1f + MathF.Sqrt(count - 8) * 0.1f)));
                    }
                    else
                    { num3 = 0.5f; }
                }

                float num4 = ((prisoner.HeroObject != null) ? (num + num2) : 0f);
                //mod 0.8f // vanila 0.25f
                //          int num5 = (int)(((float)troopRecruitmentCost + num4) * ((!prisoner.IsHero) ? 0.25f : 1f) * num3);
                int num5 = (int)(((float)troopRecruitmentCost + num4) * ((!prisoner.IsHero) ? Olto_Cost_SubModule.cost_ransom : 1f) * num3);

                if (sellerHero != null)
                {
                    if (!prisoner.IsHero)
                    {
                        if (sellerHero.GetPerkValue(DefaultPerks.Roguery.Manhunter))
                        { num5 = MathF.Round((float)num5 + (float)num5 * DefaultPerks.Roguery.Manhunter.PrimaryBonus); }
                    }
                    else if (sellerHero.IsPartyLeader && sellerHero.GetPerkValue(DefaultPerks.Roguery.RansomBroker))
                    { num5 = MathF.Round((float)num5 + (float)num5 * DefaultPerks.Roguery.RansomBroker.PrimaryBonus); }
                }

                if (num5 != 0) return num5;

                return 1;
            }
        }

    }


//Upgrade
    internal class OltoPartyTroopUpgradeModel : DefaultPartyTroopUpgradeModel
    {
        public override bool CanPartyUpgradeTroopToTarget(PartyBase upgradingParty, CharacterObject upgradeableCharacter, CharacterObject upgradeTarget)
        {
            //bool flag = this.DoesPartyHaveRequiredItemsForUpgrade(upgradingParty, upgradeTarget);
            //PerkObject perkObject;
            //bool flag2 = this.DoesPartyHaveRequiredPerksForUpgrade(upgradingParty, upgradeableCharacter, upgradeTarget, out perkObject);
            //return IsTroopUpgradeable(upgradingParty, upgradeableCharacter) && upgradeableCharacter.UpgradeTargets.Contains(upgradeTarget) && flag2 && flag;
            return IsTroopUpgradeable(upgradingParty, upgradeableCharacter) && upgradeableCharacter.UpgradeTargets.Contains(upgradeTarget);
        }

        public override int GetXpCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
        {
            if (upgradeTarget != null && characterObject.UpgradeTargets.Contains(upgradeTarget))
            {
                return (int)(Olto_Cost_SubModule.cost_xp_up * MathF.Pow(2, (upgradeTarget.Level + 4) / 5));

//                    int tier = upgradeTarget.Tier;
//                    int num = 0;
//                    for (int i = characterObject.Tier + 1; i <= tier; i++) {
//                        if (i <= 1) 	   { num += 100; }
//                        else if (i == 2) { num += 300; num += 200; }
//                        else if (i == 3) { num += 550; num += 400; }
//                        else if (i == 4) { num += 900; num += 800; }
//                        else if (i == 5) { num += 1300; num += 1600; }
//                        else if (i == 6) { num += 1700; num += 3200; }
//                        else if (i == 7) { num += 2100; num += 6400; }
//                        else { int num2 = upgradeTarget.Level + 4; num += (int)(1.333f * (float)num2 * (float)num2);                            
            }
            return 100000000;
        }

            public override int GetGoldCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
            {
                PartyWageModel partyWageModel = Campaign.Current.Models.PartyWageModel;
                int troopRecruitmentCost = partyWageModel.GetTroopRecruitmentCost(upgradeTarget, null, true);
                int troopRecruitmentCost2 = partyWageModel.GetTroopRecruitmentCost(characterObject, null, true);
 //Gangster to Bandit, add CaravanGuard
                bool flag = characterObject.Occupation == Occupation.Mercenary || characterObject.Occupation == Occupation.Bandit || characterObject.Occupation == Occupation.CaravanGuard; // || characterObject.Occupation == Occupation.Gangster;
                ExplainedNumber explainedNumber = new ExplainedNumber((float)(troopRecruitmentCost - troopRecruitmentCost2) / 2f ); //((!flag) ? 2f : 3f)

                if (party.MobileParty.HasPerk(DefaultPerks.Steward.SoundReserves, false))
                    PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.SoundReserves, party.MobileParty, true, ref explainedNumber);
                    
                if (characterObject.IsRanged && party.MobileParty.HasPerk(DefaultPerks.Bow.RenownedArcher, true))
                    PerkHelper.AddPerkBonusForParty(DefaultPerks.Bow.RenownedArcher, party.MobileParty, false, ref explainedNumber);
                    
                if (characterObject.IsInfantry && party.MobileParty.HasPerk(DefaultPerks.Throwing.ThrowingCompetitions, false))
                    PerkHelper.AddPerkBonusForParty(DefaultPerks.Throwing.ThrowingCompetitions, party.MobileParty, true, ref explainedNumber);
                    
                if (characterObject.IsMounted && PartyBaseHelper.HasFeat(party, DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat))
                    explainedNumber.AddFactor(DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat.EffectBonus, GameTexts.FindText("str_culture", null));

//SturgianRecruitUpgradeFeat for non Mounted
//              else if (characterObject.IsInfantry && PartyBaseHelper.HasFeat(party, DefaultCulturalFeats.SturgianRecruitUpgradeFeat))
                else if (PartyBaseHelper.HasFeat(party, DefaultCulturalFeats.SturgianRecruitUpgradeFeat))
                {
                    if (Olto_Cost_SubModule.hire_discount == false) explainedNumber.AddFactor(DefaultCulturalFeats.SturgianRecruitUpgradeFeat.EffectBonus, GameTexts.FindText("str_culture", null));
                    else { explainedNumber.AddFactor(DefaultCulturalFeats.SturgianRecruitUpgradeFeat.EffectBonus * 0.4f, GameTexts.FindText("str_culture", null)); }
                }

                if (flag && party.MobileParty.HasPerk(DefaultPerks.Steward.Contractors, false))
                    PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.Contractors, party.MobileParty, true, ref explainedNumber);
                    
                return (int)explainedNumber.ResultNumber;
            }


//up without horse //ItemCategory protected, need harmony (like GetYourOwnHorse mod)


// up without VeteransRespect
        public override bool DoesPartyHaveRequiredPerksForUpgrade(PartyBase party, CharacterObject character, CharacterObject upgradeTarget, out PerkObject requiredPerk)
        {
            requiredPerk = null;
            if (character.Culture.IsBandit && !upgradeTarget.Culture.IsBandit)
            {
                requiredPerk = DefaultPerks.Leadership.VeteransRespect;
//              return party.MobileParty.HasPerk(requiredPerk, checkSecondaryRole: true);
                return true;
            }
            return true;
        }

    }
}
