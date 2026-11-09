using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
namespace ArtEmergedStudios.Awake.Opening
{
    // Immutable copy of mechanics, never a conversation summary or scene-number inference.
    public sealed class CourtMemorySnapshot
    {
        public const int SchemaVersion=1;
        public string SessionId{get;}
        public IReadOnlyDictionary<string,string> Facts{get;}
        public CourtMemorySnapshot(string sessionId,IDictionary<string,string> facts)
        {SessionId=sessionId;Facts=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>(facts));}
        public string Value(string key)=>Facts.TryGetValue(key,out var v)?v:"Unknown";
        public string[] RecapKeys()
        {
            var keys=new List<string>();
            if(Value("scale_transformation_applied")=="True")
            {
                string choice=Value("mushroom_choice");
                if(choice=="Expansion"||choice=="Contraction")
                    keys.Add((Value("mushroom_random_by_wonderland")=="True"?"magic-":"")+choice.ToLowerInvariant());
            }
            if(Value("lock_open")=="True")keys.Add("key");
            if(int.TryParse(Value("ink_seeds_released"),out int seeds)&&seeds>0)keys.Add("petal");
            else if(int.TryParse(Value("butterfly_actual_landings"),out int landings)&&landings>0)keys.Add("butterfly");
            string tea=Value("tea_choice");
            if(Value("hatter_prank_revealed")=="True")keys.Add("hatter-prank");
            if(tea=="GoldLetTimeContinue"||tea=="TealTurnTimeBack")
                keys.Add((Value("tea_choice_random_by_wonderland")=="True"?"magic-":"")+(tea=="GoldLetTimeContinue"?"gold":"teal"));
            return keys.ToArray();
        }
        public string Context()=>"AWAKE_COURT_MEMORY schema="+SchemaVersion+"; session="+SessionId+"; "+string.Join("; ",Facts.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value))+"; This immutable record confirms observations only. Unknown is not failure. No names, Alice declaration, emotions or motives were recorded; never invent them.";
        public static string TeaChoice(TeaProgress tea)=>tea!=null&&(tea.Phase==TeaPhase.Rewind||tea.Phase==TeaPhase.Complete)?tea.AlternativeRule?"TealTurnTimeBack":"GoldLetTimeContinue":"Unknown";
        public static CourtMemorySnapshot Capture(CheshireSceneDirector scene)
        {
            var t=scene.tea;var o=t.opening;var garden=t.caterpillar.garden;var state=o.Session;
            var facts=new Dictionary<string,string>();
            void Add(string key,object value)=>facts[key]=value is float f?f.ToString("0.###",CultureInfo.InvariantCulture):value?.ToString()??"Unknown";
            Add("watch_physically_received",o.Model.PhysicallyReceived);
            Add("mushroom_choice",garden.scene.Model.Choice);Add("mushroom_choice_source",garden.scene.ChoiceSource);Add("mushroom_random_by_wonderland",garden.scene.ChoiceRandomByMagic);
            Add("scale_transformation_applied",garden.Model.TransformationApplied);
            Add("historical_world_scale",garden.Model.TransformationApplied?garden.Model.Target:1);
            Add("normal_scale_restored",scene.NormalScaleRestored);
            Add("key_physically_inserted",garden.scene.unlock.Magic.PhysicallyInserted);
            Add("lock_open",garden.scene.unlock.Model.IsOpen);
            Add("lock_outcome",garden.scene.unlock.Model.Outcome);
            Add("lock_magic_completed",garden.scene.unlock.Magic.IsComplete);
            Add("butterfly_actual_landings",state.ButterflyLandings);
            Add("identity_committed",state.IdentityCommitted);Add("identity_motif",state.IdentityMotif);Add("identity_source",state.IdentitySource);
            int observed=0,invited=0,touched=0,seeds=0;
            foreach(var flower in garden.interactions.flowers)
            {
                var m=state.GardenFlower(flower.flowerId);if(m.Observed)observed++;if(m.Invited)invited++;if(m.Touched)touched++;if(m.SeedReleased)seeds++;
            }
            Add("flowers_observed",observed);Add("flowers_invited",invited);Add("petals_physically_touched",touched);Add("ink_seeds_released",seeds);
            Add("tea_outcome",t.Model.Outcome);Add("tea_received_particles",t.Model.PeakReceivedParticles);Add("tea_observed_spills",t.Model.SpilledParticles);
            Add("hatter_prank_started",t.Model.PrankStarted);Add("hatter_prank_completed",t.Model.PrankCompleted);Add("hatter_prank_revealed",t.Model.PrankRevealed);
            Add("tea_items_caught",false);Add("prank_start_assisted",t.Model.PrankStartAssisted);
            Add("cup_physically_received",t.Model.CupReceived);Add("cup_provided_by_magic",t.Model.CupProvidedByMagic);
            Add("cup_rest_assisted",t.Model.CupRestAssisted);Add("tea_pour_assistance",t.Model.AssistanceRequested);
            Add("tea_choice_random_by_wonderland",t.ease!=null&&t.ease.RandomRingChoice);Add("tea_choice",TeaChoice(t.Model));Add("tea_choice_placement_assisted",t.Model.RulePlacementAssisted);
            Add("rewind_completed",t.Model.Phase==TeaPhase.Complete&&t.Model.AlternativeRule);
            Add("help_events",o.narrator.ease!=null?string.Join("|",o.narrator.ease.Events):"Unknown");
            return new CourtMemorySnapshot(Guid.NewGuid().ToString("N"),facts);
        }
    }
    public enum StorySealOutcome{Unresolved,PhysicalTouch,Assisted,Declined}
    public sealed class StorySealProgress
    {
        public StorySealOutcome Outcome{get;private set;}
        public bool AssistanceEnabled{get;private set;}
        public int FailedAttempts=>attempts.Count;
        public bool Attempting{get;private set;}
        public bool Armed{get;private set;}
        public bool Resolved=>Outcome!=StorySealOutcome.Unresolved;
        readonly TeaAttemptCounter attempts=new TeaAttemptCounter();float dwell;
        public void RequestHelp(){if(!Resolved)AssistanceEnabled=true;}
        public void Sample(bool valid,bool near,bool contact,float dt)
        {
            if(Resolved)return;
            if(!valid){Armed=false;dwell=0;Attempting=false;attempts.Sample(false,false,false,dt);return;}
            if(!near)Armed=true;
            Attempting=Armed&&near;
            dwell=Armed&&contact?dwell+UnityEngine.Mathf.Clamp(dt,0,.05f):0;
            if(dwell>=.12f){Outcome=StorySealOutcome.PhysicalTouch;return;}
            attempts.Sample(true,Attempting,false,dt);if(attempts.Due)AssistanceEnabled=true;
        }
        public bool ExplicitFinishForMe(){if(Resolved)return false;AssistanceEnabled=true;Outcome=StorySealOutcome.Assisted;return true;}
        public bool ExplicitDecline(){if(Resolved)return false;Outcome=StorySealOutcome.Declined;return true;}
    }
}
