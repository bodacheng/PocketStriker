using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using FightScene;
using UnityEngine;
using UnityEngine.EventSystems;

public static partial class PocketStrikerBattleCameraSmoke
{
    [Serializable] public sealed class ImpactReport
    {
        public bool complete, passed;
        public List<string> checks = new List<string>();
        public List<string> failures = new List<string>();
        public string label;
        public int livePointerChecks;
        public bool groupManualControlsDisabled;
        public List<CrowdBodySample> crowd = new List<CrowdBodySample>();
        public List<ContactSample> contacts = new List<ContactSample>();
        public List<DashSymbolSample> dashSymbols = new List<DashSymbolSample>();
        public string scope = "Actual loaded FightScene, animated characters, production skill state/animation events, hit queries and Rigidbody solver. Controlled placement, manual skill selection and invulnerability isolate movement; no synthetic damage is injected. Local Self account, no backend/reward/story requests.";
        public List<HudSample> hud = new List<HudSample>();
        public List<BodySample> bodies = new List<BodySample>();
        public List<ImpactCase> cases = new List<ImpactCase>();
    }
    [Serializable] public sealed class CrowdBodySample
    {
        public string id;
        public int frames, settledFrames;
        public float maxStep, maxSpeed, settledMaxStep, settledMaxSpeed, settledMaxPenetration;
        public float settledMinimumTorsoGap = float.MaxValue;
        public Vector3 start, end;
    }
    [Serializable] public sealed class HudSample
    {
        public string name, process, focus;
        public bool dashVisible, dreamVisible, dashInteractable, dreamInteractable;
        public float expected, shown, materialShown;
        public bool oldFocusIgnored;
    }
    [Serializable] public sealed class BodySample
    {
        public string id, model;
        public Vector3 scale, modelBounds;
        public float mass, bodyHeight, bodyRadius, referenceVisualHeight, referenceTorsoRadius, torsoContactRadius;
        public Vector2 referenceTorsoSize;
        public bool mainBodySolid, allLimbsTriggers, active;
        public int solverIterations, solidBodies;
        public string collisionMode;
        public List<string> colliders = new List<string>();
    }
    [Serializable] public sealed class ImpactCase
    {
        public string name;
        public int firstHits, lastHits, hurtFrames, peakHits;
        public bool sourceDied, victimDied, collisionRestored;
        public float initialKnockOffGauge;
        public string victimPreviousState;
        public float maxVictimSpeed, maxAttackerSpeed, maxFrameStep, minimumSeparation = float.MaxValue;
        public Vector3 attackerStart, attackerEnd, victimStart, victimEnd;
        public List<ImpactFrame> frames = new List<ImpactFrame>();
        public List<string> screenshots = new List<string>();
    }
    [Serializable] public sealed class ImpactFrame
    {
        public float time;
        public Vector3 attacker, victim, attackerVelocity, victimVelocity;
        public string attackerState, victimState;
        public int hits;
        public bool attackerDead, victimDead, followingImpact, victimGrounded;
        public float victimHp;
        public float bodyPenetration, torsoGap;
    }
    [Serializable] public sealed class ContactSample
    {
        public string name, player, enemy;
        public float rootSeparation, torsoGap, maxSettledStep, maxPenetration;
        public List<string> screenshots = new List<string>();
    }
    [Serializable] public sealed class DashSymbolSample
    {
        public string phase, screenshot, texture;
        public bool overlayDisabled, personVisible, personParticleSuppressed, interactable, held;
    }
    static ImpactReport impact;

    static async UniTask ReviewImpact(UnitInfo leader)
    {
        impact = new ImpactReport {label=Environment.GetEnvironmentVariable("POCKETSTRIKER_IMPACT_REVIEW")};
        report.scope = impact.scope;
        report.limitation = "Controlled attack inputs/placements and invulnerability, local account; editor physics evidence, not unmodified player input or device frame-rate certification.";
        UnityEngine.Random.InitState(842);
        var fixture = CreateOrdinary(leader, TeamMode.Rotation, 3);
        foreach (var unit in fixture.FightMembers.HeroSets.GetValues().Concat(fixture.FightMembers.EnemySets.GetValues()))
        { unit.set.a1 = "145"; unit.set.a2 = "148"; unit.set.a3 = "150"; }
        FightLoad.Go(fixture);
        await ImpactStart(3, false, "opening");
        foreach (var unit in RTFightManager.Target.team1.teamMembers.GetValues()) AuditBody(unit);
        await ReviewCloseContact("9-vs-9");
        await SkillImpact("145-hit", "145", 1.25f);
        await SkillImpact("145-evade-miss", "145", 1.25f, evade: true);
        await SkillImpact("148-hit", "148", 1.25f);
        await SkillImpact("150-hit", "150", 1.25f);
        await SkillImpact("145-accumulated-knockoff", "145", 1.25f,forceKnockoff:true);

        await SkillImpact("145-victim-death", "145", 1.25f, killVictim: true);
        await UniTask.WaitUntil(()=>RTFightManager.Target.team2.RMode_Unit.Value != null && !RTFightManager.Target.team2.RMode_Unit.Value.FightDataRef.IsDead.Value).Timeout(TimeSpan.FromSeconds(12));
        await SkillImpact("145-attacker-death", "145", 1.25f, killSource: true);
        await UniTask.WaitUntil(()=>RTFightManager.Target.team1.RMode_Unit.Value != null && !RTFightManager.Target.team1.RMode_Unit.Value.FightDataRef.IsDead.Value).Timeout(TimeSpan.FromSeconds(12));
        var manager = RTFightManager.Target;
        var before = manager.team1.RMode_Unit.Value;
        var next = manager.team1.teamMembers.GetValues().First(u=>u!=before && !u.FightDataRef.IsDead.Value);
        next.FightDataRef.DreamComboGauge.Value = FightGlobalSetting.DreamComboGaugeMax / 2;
        ClickPortrait(next);
        await UniTask.WaitUntil(()=>manager.team1.RMode_Unit.Value==next && GetHUD().InputsManager.CurrentFocus.Value==next).Timeout(TimeSpan.FromSeconds(8));
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        SampleHUD("reserve-half");
        before.FightDataRef.DreamComboGauge.Value = FightGlobalSetting.DreamComboGaugeMax;
        var sample = SampleHUD("old-reserve-energy");
        sample.oldFocusIgnored = GetHUD().InputsManager.CurrentFocus.Value == next && Mathf.Abs(sample.shown-.5f)<.01f;
        await CheckLiveActionPointers("reserve");
        await SkillImpact("145-after-reserve", "145", 1.25f);
        await SkillImpact("145-natural-victim-death", "145", 1.25f, naturalDeath: true);
        await UniTask.WaitUntil(()=>manager.team2.RMode_Unit.Value != null && !manager.team2.RMode_Unit.Value.FightDataRef.IsDead.Value).Timeout(TimeSpan.FromSeconds(12));
        next._MyBehaviorRunner.ChangeState("Death");
        await UniTask.WaitUntil(()=>manager.team1.RMode_Unit.Value!=next && manager.team1.RMode_Unit.Value!=null).Timeout(TimeSpan.FromSeconds(12));
        SampleHUD("death-replacement");
        HoldTeams();
        FightLoad.Go(FightLoad.Fight, true); await UniTask.NextFrame();
        await ImpactStart(3, false, "retry");

        foreach (var pair in new[]{new[]{"9","18"},new[]{"8","11"},new[]{"13","16"}})
        {
            var contactFixture=CreateOrdinary(leader,TeamMode.Rotation,1);
            contactFixture.FightMembers.HeroSets.Get(0,0).r_id=pair[0];
            contactFixture.FightMembers.EnemySets.Get(0,0).r_id=pair[1];
            foreach(var unit in contactFixture.FightMembers.HeroSets.GetValues().Concat(contactFixture.FightMembers.EnemySets.GetValues()))
            {unit.set.a1="145";unit.set.a2="148";unit.set.a3="150";}
            FightLoad.Go(contactFixture);
            await UniTask.WaitUntil(()=>IsLoaded(1,1,false)&&FSceneProcessesRunner.Main.currentProcess is FightingProcess
                &&RTFightManager.Target.team1.RMode_Unit.Value?.UnitInfo.r_id==pair[0]
                &&RTFightManager.Target.team2.RMode_Unit.Value?.UnitInfo.r_id==pair[1]).Timeout(TimeSpan.FromSeconds(180));
            RTFightManager.Target.team1.TurnAllUnitsInvincible(true);RTFightManager.Target.team2.TurnAllUnitsInvincible(true);
            HoldTeams();
            foreach(var unit in RTFightManager.Target.team1.teamMembers.GetValues().Concat(RTFightManager.Target.team2.teamMembers.GetValues()))AuditBody(unit);
            string pairName=pair[0]+"-vs-"+pair[1];
            await ReviewCloseContact(pairName);
            foreach(string skill in new[]{"145","148","150"})await SkillImpact(pairName+"-"+skill,skill,1.25f);
        }
        var multi = CreateOrdinary(leader, TeamMode.MultiRaid, 3);
        string[] models = {"3","15","8"};
        for (int i=0;i<3;i++)
        {
            multi.FightMembers.HeroSets.Get(0,i).r_id=models[i];
            multi.FightMembers.EnemySets.Get(0,i).r_id=models[2-i];
        }
        FightLoad.Go(multi);
        await ImpactStart(3, false, "multi");
        manager=RTFightManager.Target;
        foreach(var unit in manager.team1.teamMembers.GetValues()) AuditBody(unit);
        HoldTeams();
        var all=manager.team1.teamMembers.GetValues().Concat(manager.team2.teamMembers.GetValues()).ToArray();
        for(int i=0;i<all.Length;i++)
        {
            var unit=all[i];var root=unit.WholeT;
            // Deliberate starting penetration for the solver stress fixture, not a skill teleport.
            var crowded = new Vector3((i%3-1)*.45f,0,(i/3-.5f)*.45f);
            unit._BasicPhysicSupport.Rigidbody.position=crowded; root.position=crowded;
            root.rotation=Quaternion.Euler(0,i<3?0:180,0);
            unit._BasicPhysicSupport.Rigidbody.constraints=RigidbodyConstraints.FreezeRotation;
            unit._BasicPhysicSupport.OpenEnemyTouchingDrag(1);
        }
        Physics.SyncTransforms();
        await UniTask.WhenAll(RecordImpact("crowded-six",all[0],all[3],5), RecordCrowd(all,5));
        var group=ScriptableObject.CreateInstance<GangbangInfo>();
        Setup(group,TeamMode.MultiRaid);group.FightMembers=Members(leader);
        group.ApplyTeamLimit(24);group.ConvertTeamToGangbang();
        var roster=Units.Dic.Keys.OrderBy(id=>int.TryParse(id,out var number)?number:int.MaxValue).ToArray();
        int rosterIndex=0;
        foreach(var unit in group.FightMembers.HeroSets.GetValues())unit.r_id=roster[rosterIndex++%roster.Length];
        rosterIndex=0;
        foreach(var unit in group.FightMembers.EnemySets.GetValues())unit.r_id=roster[rosterIndex++%roster.Length];
        FightLoad.Go(group);
        await ImpactStart(24,true,"group-countdown");
        foreach(var unit in RTFightManager.Target.team1.teamMembers.GetValues().Concat(RTFightManager.Target.team2.teamMembers.GetValues()))AuditBody(unit);
        ValidateImpact(roster);
        impact.complete=true; impact.passed=impact.failures.Count==0; SaveImpact();
        Require(impact.passed,"Impact/HUD/body runtime checks failed: "+string.Join("; ",impact.failures));
    }

    static async UniTask ImpactStart(int count, bool group, string label)
    {
        await UniTask.WaitUntil(()=>IsLoaded(count,count,group)&&FSceneProcessesRunner.Main.currentProcess is CountDownProcess).Timeout(TimeSpan.FromSeconds(180));
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        SampleHUD(label+"-initial");
        await CaptureDashSymbol(label+"-disabled");
        var center=RTFightManager.Target.team1.RMode_Unit.Value ?? RTFightManager.Target.team1.teamMembers.GetValues().First();
        foreach (float amount in new[]{0f,.35f,1f})
        {
            center.FightDataRef.DreamComboGauge.Value=Mathf.RoundToInt(FightGlobalSetting.DreamComboGaugeMax*amount);
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            SampleHUD(label+"-energy-"+amount);
            string energyCapture = Path.GetFullPath(Path.Combine(Output,label+"-energy-"+Mathf.RoundToInt(amount*100)+".png"));
            ScreenCapture.CaptureScreenshot(energyCapture);
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        }
        await CaptureDashSymbol(label+"-disabled-settled");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Output,label+"-countdown.png")));
        await UniTask.WaitUntil(()=>FSceneProcessesRunner.Main.currentProcess is FightingProcess).Timeout(TimeSpan.FromSeconds(15));
        RTFightManager.Target.team1.TurnAllUnitsInvincible(true);
        RTFightManager.Target.team2.TurnAllUnitsInvincible(true);
        HoldTeams();
        if(group)
        {
            var input=GetHUD().InputsManager;
            impact.groupManualControlsDisabled=input.CurrentFocus.Value==null
                &&!input.DashButton.gameObject.activeInHierarchy&&!input.DreamComboBtn.gameObject.activeInHierarchy
                &&!input.MovementJoystick.gameObject.activeInHierarchy;
            Require(impact.groupManualControlsDisabled,"Group must close manual actions after countdown");
        }
        else await CheckLiveActionPointers(label);
    }

    static async UniTask CheckLiveActionPointers(string label)
    {
        // Dispatch the real authored callbacks through the actual EventSystem hit,
        // including the enlarged edge previously covered by the movement joystick.
        var input = GetHUD().InputsManager;
        Canvas.ForceUpdateCanvases();
        foreach (var button in new[] {input.DashButton, input.DreamComboBtn})
        foreach (bool edge in new[] {false,true})
        {
            var rect = button.targetGraphic.rectTransform;
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var local = edge ? new Vector3(rect.rect.xMin - 12, rect.rect.center.y) : (Vector3)rect.rect.center;
            var pointer = new PointerEventData(EventSystem.current) {position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(local)),button=PointerEventData.InputButton.Left};
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer,hits);
            Require(hits.Count>0,label+": no live action raycast");
            var hit=hits[0].gameObject;
            Require(hit==button.gameObject || hit.transform.IsChildOf(button.transform),label+": live action covered by "+hit.name);
            ExecuteEvents.ExecuteHierarchy(hit,pointer,ExecuteEvents.pointerDownHandler);
            Require(button==input.DashButton?input.acc:input.dreamCombo,label+": live action down did not reach production input");
            if(button==input.DashButton&&!edge)await CaptureDashSymbol(label+"-pressed");
            Require(!input.MovementJoystick.GetJoystickState(),label+": action leaked into movement joystick");
            ExecuteEvents.ExecuteHierarchy(hit,pointer,ExecuteEvents.pointerUpHandler);
            Require(!input.acc&&!input.dreamCombo,label+": live action pointer up did not release");
            impact.livePointerChecks++;
            if(button==input.DashButton&&!edge)await CaptureDashSymbol(label+"-normal");
        }
        SaveImpact();
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
    }

    static async UniTask CaptureDashSymbol(string phase)
    {
        var input=GetHUD().InputsManager;
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var groups=(System.Collections.IDictionary)typeof(MobileInputsManager).GetField("_elementEffects",flags).GetValue(input);
        var element=typeof(MobileInputsManager).GetField("_focusing",flags).GetValue(input);
        var group=groups.Contains(element)?groups[element]:null;
        var rush=group!=null?(ParticleSystem)typeof(ElementEffectsGroup).GetField("_rushBtn",flags).GetValue(group):null;
        var particleRenderers=rush!=null?rush.GetComponentsInChildren<ParticleSystemRenderer>(true):Array.Empty<ParticleSystemRenderer>();
        bool personParticleSuppressed=particleRenderers.Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.mainTexture!=null&&m.mainTexture.name=="rush")).All(r=>!r.enabled);
        var glyph=input.DashButton.GetComponentInChildren<BattleActionGlyph>(true);
        string path=Path.GetFullPath(Path.Combine(Output,"dash-"+phase+".png"));
        impact.dashSymbols.Add(new DashSymbolSample{phase=phase,screenshot=path,texture=glyph?.mainTexture?.name,
            overlayDisabled=glyph!=null&&glyph.ShowsSprintPerson,personVisible=glyph!=null&&glyph.ShowsSprintPerson,
            personParticleSuppressed=personParticleSuppressed,interactable=input.DashButton.interactable,held=input.acc});
        ScreenCapture.CaptureScreenshot(path);
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        SaveImpact();
    }

    static async UniTask ReviewCloseContact(string name)
    {
        HoldTeams();PlaceDuel(.3f,0);
        var a=RTFightManager.Target.team1.RMode_Unit.Value;var v=RTFightManager.Target.team2.RMode_Unit.Value;
        foreach(var unit in new[]{a,v})
        {unit._BasicPhysicSupport.Rigidbody.constraints=RigidbodyConstraints.FreezeRotation;unit._BasicPhysicSupport.OpenEnemyTouchingDrag(1);}
        var pa=a._BasicPhysicSupport.GetComponent<CombatBodyProfile>();var pv=v._BasicPhysicSupport.GetComponent<CombatBodyProfile>();
        var row=new ContactSample{name=name,player=a.UnitInfo.r_id,enemy=v.UnitInfo.r_id};impact.contacts.Add(row);
        float start=Time.realtimeSinceStartup;var previous=v.WholeT.position;
        while(Time.realtimeSinceStartup-start<3)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            float step=Vector3.Distance(previous,v.WholeT.position);previous=v.WholeT.position;
            if(Time.realtimeSinceStartup-start<2)continue;
            row.maxSettledStep=Mathf.Max(row.maxSettledStep,step);
            if(Physics.ComputePenetration(pa.Body,pa.Body.transform.position,pa.Body.transform.rotation,pv.Body,pv.Body.transform.position,pv.Body.transform.rotation,out _,out var depth))
                row.maxPenetration=Mathf.Max(row.maxPenetration,depth);
        }
        row.rootSeparation=Vector3.Distance(a.WholeT.position,v.WholeT.position);row.torsoGap=pa.PlanarTorsoGap(pv);
        var normal=Path.GetFullPath(Path.Combine(Output,"contact-"+name+"-normal.png"));
        ScreenCapture.CaptureScreenshot(normal);row.screenshots.Add(normal);
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        var rig=new GameObject("Contact shape review overlay");
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        foreach(var pair in new[]{pa,pv})
        for(int meridian=0;meridian<12;meridian++)
        {
            var lineNode=new GameObject("Contact meridian");lineNode.transform.SetParent(rig.transform);
            var line=lineNode.AddComponent<LineRenderer>();line.sharedMaterial=material;
            line.startColor=line.endColor=pair==pa?Color.cyan:new Color(1,.65f,.2f);
            line.startWidth=line.endWidth=.015f;line.positionCount=33;line.useWorldSpace=true;
            float angle=meridian*Mathf.PI/6;
            for(int i=0;i<33;i++)
            {
                float y=pair.Height*i/32;float radius=pair.ContactRadiusAtWorldHeight(pair.transform.position.y+y);
                line.SetPosition(i,pair.transform.position+new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius));
            }
        }
        var wire=Path.GetFullPath(Path.Combine(Output,"contact-"+name+"-wire.png"));
        ScreenCapture.CaptureScreenshot(wire);row.screenshots.Add(wire);
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        UnityEngine.Object.Destroy(rig);UnityEngine.Object.Destroy(material);SaveImpact();
    }

    static async UniTask RecordCrowd(Data_Center[] units,float seconds)
    {
        var previous=units.Select(u=>u.WholeT.position).ToArray();
        var rows=units.Select((u,i)=>new CrowdBodySample{id=u.UnitInfo.r_id+"/"+i,start=previous[i]}).ToArray();
        impact.crowd.AddRange(rows);
        float start=Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup-start<seconds)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            bool settled=Time.realtimeSinceStartup-start>3;
            for(int i=0;i<units.Length;i++)
            {
                var unit=units[i];var row=rows[i];var current=unit.WholeT.position;
                float step=Vector3.Distance(previous[i],current);previous[i]=current;
                row.frames++;row.maxStep=Mathf.Max(row.maxStep,step);row.maxSpeed=Mathf.Max(row.maxSpeed,unit._BasicPhysicSupport.Rigidbody.linearVelocity.magnitude);row.end=current;
                if(!settled)continue;
                row.settledFrames++;row.settledMaxStep=Mathf.Max(row.settledMaxStep,step);
                row.settledMaxSpeed=Mathf.Max(row.settledMaxSpeed,unit._BasicPhysicSupport.Rigidbody.linearVelocity.magnitude);
                var body=unit._BasicPhysicSupport.GetComponent<CombatBodyProfile>().Body;
                for(int j=0;j<units.Length;j++)
                {
                    var other=units[j]._BasicPhysicSupport.GetComponent<CombatBodyProfile>().Body;
                    if(i!=j)row.settledMinimumTorsoGap=Mathf.Min(row.settledMinimumTorsoGap,unit._BasicPhysicSupport.GetComponent<CombatBodyProfile>().PlanarTorsoGap(units[j]._BasicPhysicSupport.GetComponent<CombatBodyProfile>()));
                    if(i==j||Physics.GetIgnoreLayerCollision(body.gameObject.layer,other.gameObject.layer)||Physics.GetIgnoreCollision(body,other))continue;
                    if(Physics.ComputePenetration(body,body.transform.position,body.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out var depth))
                        row.settledMaxPenetration=Mathf.Max(row.settledMaxPenetration,depth);
                }
            }
        }
        SaveImpact();
    }

    static HudSample SampleHUD(string name)
    {
        var input=GetHUD().InputsManager; var focus=input.CurrentFocus.Value;
        var gauge=input.DreamComboGauge;
        var row=new HudSample{name=name,process=FSceneProcessesRunner.Main.currentProcess.GetType().Name,focus=focus?.UnitInfo?.r_id,
            dashVisible=input.DashButton.gameObject.activeInHierarchy,dreamVisible=input.DreamComboBtn.gameObject.activeInHierarchy,
            dashInteractable=input.DashButton.interactable,dreamInteractable=input.DreamComboBtn.interactable,
            expected=focus!=null?(float)focus.FightDataRef.DreamComboGauge.Value/FightGlobalSetting.DreamComboGaugeMax:-1,
            shown=1-gauge.RemoveSegments.Value/Mathf.Max(1,gauge.SegmentCount.Value),
            materialShown=1-gauge.GetComponent<UnityEngine.UI.Image>().material.GetFloat("_RemoveSegments")/Mathf.Max(1,gauge.SegmentCount.Value)};
        impact.hud.Add(row); SaveImpact(); return row;
    }

    static void AuditBody(Data_Center center)
    {
        var row=new BodySample{id=center.UnitInfo.r_id,model=center.name,scale=center.WholeT.lossyScale,mass=center._BasicPhysicSupport.Rigidbody.mass,active=center.WholeT.gameObject.activeInHierarchy};
        var profile=center._BasicPhysicSupport.GetComponent<CombatBodyProfile>();
        if(profile!=null){row.bodyHeight=profile.Height;row.bodyRadius=profile.Radius;row.referenceTorsoSize=profile.ReferenceTorsoSize;row.referenceTorsoRadius=profile.ReferenceTorsoRadius;row.referenceVisualHeight=profile.ReferenceVisualHeight;row.torsoContactRadius=profile.ContactRadiusAtWorldHeight(profile.TorsoCenter.y);row.mainBodySolid=profile.Body!=null&&!profile.Body.isTrigger;}
        row.allLimbsTriggers=center.WholeT.GetComponentsInChildren<BO_Limb>(true).All(limb=>(limb.myColliderMustEquip??limb.GetComponent<Collider>())?.isTrigger==true);
        row.solidBodies=center.WholeT.GetComponentsInChildren<Collider>(true).Count(c=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&c.attachedRigidbody==center._BasicPhysicSupport.Rigidbody);
        row.solverIterations=center._BasicPhysicSupport.Rigidbody.solverIterations;
        row.collisionMode=center._BasicPhysicSupport.Rigidbody.collisionDetectionMode.ToString();
        var renderers=center.WholeT.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if(renderers.Length>0){var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);row.modelBounds=bounds.size;}
        foreach(var limb in center.WholeT.GetComponentsInChildren<BO_Limb>(true))
        {
            var c=limb.myColliderMustEquip??limb.GetComponent<Collider>();
            if(c!=null)row.colliders.Add(c.name+" "+c.GetType().Name+" trigger="+c.isTrigger+" size="+c.bounds.size);
        }
        impact.bodies.Add(row); SaveImpact();
    }

    static async UniTask SkillImpact(string name,string skillId,float separation,bool evade=false,bool killSource=false,bool killVictim=false,bool naturalDeath=false,bool forceKnockoff=false)
    {
        HitBoxesProcesser.Instance.AllProcessingFade();
        HoldTeams(); PlaceDuel(separation,0);
        var attacker=RTFightManager.Target.team1.RMode_Unit.Value;var victim=RTFightManager.Target.team2.RMode_Unit.Value;
        foreach(var unit in new[]{attacker,victim})
        {unit._BasicPhysicSupport.Rigidbody.constraints=RigidbodyConstraints.FreezeRotation;unit.FightDataRef.Resistance.Value=0;unit.FightDataRef.Invincible=true;}
        await UniTask.Delay(400);
        // Isolate a fresh neutral reaction. A forced Empty after the previous
        // knockoff leaves LastState=KnockOff and intentionally redirects the next
        // hit. Exercise accumulation separately below rather than mixing cases.
        foreach(var unit in new[]{attacker,victim})
        {unit._MyBehaviorRunner.ChangeState("Move");unit._MyBehaviorRunner.ChangeState("Empty");}
        victim.FightDataRef.GetKnockOffCount().SetGauge(forceKnockoff?FightGlobalSetting.KnockOffExtent+3:0);
        // Each authored 145 strike deals .175 in this local roster. Survive the
        // first strike, then die to the next real hit while the weapon stays active.
        if(naturalDeath){victim.FightDataRef.CurrentHp.Value=.25f;victim.FightDataRef.Invincible=false;}
        var entry=attacker._MyBehaviorRunner.SkillEntityDic.First(pair=>pair.Value.SkillID==skillId);
        attacker._MyBehaviorRunner.ChangeState(entry.Key);
        bool actionDone=false;
        await RecordImpact(name,attacker,victim,4, elapsed=> {
            if (actionDone || elapsed < (evade ? .70f : 1.0f)) return;
            actionDone=true;
            if(evade)victim._BasicPhysicSupport.SetPositionBySkill(new Vector3(-6,0,7));
            if(killSource)attacker._MyBehaviorRunner.ChangeState("Death");
            if(killVictim)victim._MyBehaviorRunner.ChangeState("Death");
        });
    }

    static async UniTask RecordImpact(string name,Data_Center attacker,Data_Center victim,float seconds,Action<float> action=null)
    {
        var item=new ImpactCase{name=name,attackerStart=attacker.WholeT.position,victimStart=victim.WholeT.position,firstHits=victim.FightDataRef.GetBeHitCount(),initialKnockOffGauge=victim.FightDataRef.GetKnockOffCount().GetGauge(),victimPreviousState=victim._MyBehaviorRunner.GetLastState().StateKey};
        impact.cases.Add(item);float start=Time.realtimeSinceStartup;float nextCapture=0;
        var previous=victim.WholeT.position;
        while(Time.realtimeSinceStartup-start<seconds)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            float elapsed=Time.realtimeSinceStartup-start;action?.Invoke(elapsed);
            var a=attacker._BasicPhysicSupport.Rigidbody;var v=victim._BasicPhysicSupport.Rigidbody;
            var sample=new ImpactFrame{time=elapsed,attacker=attacker.WholeT.position,victim=victim.WholeT.position,attackerVelocity=a.linearVelocity,victimVelocity=v.linearVelocity,
                attackerState=attacker._MyBehaviorRunner.GetNowState().StateKey,victimState=victim._MyBehaviorRunner.GetNowState().StateKey,hits=victim.FightDataRef.GetBeHitCount(), attackerDead=attacker.FightDataRef.IsDead.Value,victimDead=victim.FightDataRef.IsDead.Value,
                followingImpact=victim._BasicPhysicSupport.FollowingImpact,victimGrounded=victim._BasicPhysicSupport.hiddenMethods.Grounded,victimHp=victim.FightDataRef.CurrentHp.Value};
            var sourceProfile=attacker._BasicPhysicSupport.GetComponent<CombatBodyProfile>();
            var targetProfile=victim._BasicPhysicSupport.GetComponent<CombatBodyProfile>();
            sample.torsoGap=sourceProfile.PlanarTorsoGap(targetProfile);
            var sourceBody=sourceProfile?.Body;
            var victimBody=victim._BasicPhysicSupport.GetComponent<CombatBodyProfile>()?.Body;
            if(sourceBody!=null&&victimBody!=null&&Physics.ComputePenetration(sourceBody,sourceBody.transform.position,sourceBody.transform.rotation,victimBody,victimBody.transform.position,victimBody.transform.rotation,out _,out var depth))sample.bodyPenetration=depth;
            item.frames.Add(sample);item.peakHits=Mathf.Max(item.peakHits,sample.hits);
            if(victim.FightDataRef.GettingDamage)item.hurtFrames++;
            item.maxVictimSpeed=Mathf.Max(item.maxVictimSpeed,v.linearVelocity.magnitude);item.maxAttackerSpeed=Mathf.Max(item.maxAttackerSpeed,a.linearVelocity.magnitude);
            item.maxFrameStep=Mathf.Max(item.maxFrameStep,Vector3.Distance(previous,sample.victim));previous=sample.victim;
            item.minimumSeparation=Mathf.Min(item.minimumSeparation,Vector3.Distance(sample.attacker,sample.victim));
            if(elapsed>=nextCapture)
            {
                var path=Path.GetFullPath(Path.Combine(Output,name+"-"+item.screenshots.Count.ToString("D3")+".png"));
                ScreenCapture.CaptureScreenshot(path);item.screenshots.Add(path);nextCapture+=.12f;
            }
        }
        item.attackerEnd=attacker.WholeT.position;item.victimEnd=victim.WholeT.position;item.lastHits=victim.FightDataRef.GetBeHitCount();item.sourceDied=attacker.FightDataRef.IsDead.Value;item.victimDied=victim.FightDataRef.IsDead.Value;
        var bodyA=attacker._BasicPhysicSupport.GetComponent<CombatBodyProfile>()?.Body;
        var bodyV=victim._BasicPhysicSupport.GetComponent<CombatBodyProfile>()?.Body;
        item.collisionRestored=bodyA==null||bodyV==null||!Physics.GetIgnoreCollision(bodyA,bodyV);
        SaveImpact();
    }
    static void ValidateImpact(string[] roster)
    {
        void Check(string name,bool passed){if(passed)impact.checks.Add(name);else impact.failures.Add(name);}
        var countdown=impact.hud.Where(h=>h.process=="CountDownProcess").ToArray();
        Check("countdown-actions-visible-and-disabled",countdown.Length>=16&&countdown.All(h=>h.dashVisible&&h.dreamVisible&&!h.dashInteractable&&!h.dreamInteractable));
        Check("countdown-real-energy-and-rendered-material-match",countdown.All(h=>Mathf.Abs(h.shown-Mathf.Max(0,h.expected))<.01f&&Mathf.Abs(h.materialShown-h.shown)<.01f));
        Check("reserve-displays-new-fighter-half-energy",impact.hud.Any(h=>h.name=="reserve-half"&&Mathf.Abs(h.expected-.5f)<.01f&&Mathf.Abs(h.materialShown-.5f)<.01f));
        Check("departed-fighter-cannot-change-energy-ring",impact.hud.Any(h=>h.name=="old-reserve-energy"&&h.oldFocusIgnored));
        Check("live-action-center-and-expanded-edge-pointer-down-up",impact.livePointerChecks==16);
        Check("group-closes-manual-control-after-visible-countdown",impact.groupManualControlsDisabled);
        Check("death-and-retry-focus-bind-current-real-energy",impact.hud.Where(h=>h.name=="death-replacement"||h.name.StartsWith("retry-")).All(h=>Mathf.Abs(h.expected-h.materialShown)<.01f));
        foreach(string name in new[]{"145-hit","145-after-reserve"})
        {
            var item=impact.cases.First(c=>c.name==name);var following=item.frames.Where(f=>f.followingImpact).ToArray();
            Check(name+"-uses-stable-body-impact",following.Length>10&&item.hurtFrames>10);
            Check(name+"-maintains-body-separation",following.All(f=>{var delta=f.victim-f.attacker;delta.y=0;return delta.magnitude>=1.2f&&f.bodyPenetration<.04f;}));
        }
        foreach(string name in new[]{"148-hit","150-hit"})Check(name+"-still-connects",impact.cases.First(c=>c.name==name).hurtFrames>5);
        Check("145-evade-really-misses",impact.cases.First(c=>c.name=="145-evade-miss").hurtFrames==0);
        Check("dead-victim-never-reenters-hit",impact.cases.First(c=>c.name=="145-victim-death").frames.Any(f=>f.victimDead)
            &&!impact.cases.First(c=>c.name=="145-victim-death").frames.Any(f=>f.victimDead&&f.victimState=="Hit"));
        Check("source-death-releases-impact",impact.cases.First(c=>c.name=="145-attacker-death").frames.Where(f=>f.attackerDead&&f.time>1.1f).All(f=>!f.followingImpact));
        var natural=impact.cases.First(c=>c.name=="145-natural-victim-death");
        Check("real-skill-damage-kills-low-hp-victim-without-reentering-hit",natural.victimDied&&natural.frames.Any(f=>f.victimHp<=0)&&!natural.frames.Any(f=>f.victimDead&&(f.victimState=="Hit"||f.followingImpact)));
        Check("all-impact-pairs-restore-collision",impact.cases.All(c=>c.collisionRestored));
        Check("body-sweeps-do-not-suspend-knockback-above-ground",new[]{"145-hit","145-after-reserve"}.All(name=>impact.cases.First(c=>c.name==name).victimEnd.y<.05f));
        Check("knockback-lands-and-stays-grounded-after-release",new[]{"145-hit","145-after-reserve"}.All(name=>{var frames=impact.cases.First(c=>c.name==name).frames;var tail=frames.Where(f=>f.time>3.5f).ToArray();return frames.Any(f=>f.victim.y>.2f)&&tail.Length>5&&tail.All(f=>f.victimGrounded&&f.victim.y<.05f&&!f.followingImpact);}));
        Check("all-registered-models-audited",roster.All(id=>impact.bodies.Any(b=>b.id==id)));
        Check("one-solid-capsule-per-fighter-with-trigger-hurt-volumes",impact.bodies.All(b=>b.mainBodySolid&&b.allLimbsTriggers&&b.solidBodies==(b.active?1:0)));
        Check("visual-calibrated-body-dimensions-and-continuous-solver",impact.bodies.All(b=>b.bodyHeight>=2.79f&&b.bodyHeight<=3.41f&&b.bodyRadius>=.879f&&b.bodyRadius<=1.081f&&b.solverIterations==8&&b.collisionMode=="Continuous"));
        Check("different-size-close-contact-settles-with-visible-torso-clearance",impact.contacts.Count==4&&impact.contacts.All(c=>c.torsoGap>=-.03f&&c.maxSettledStep<.02f&&c.maxPenetration<.04f&&c.screenshots.Count==2));
        Check("different-size-145-148-150-all-connect-and-145-lands",impact.cases.Where(c=>c.name.Contains("-vs-")).Count()==9&&impact.cases.Where(c=>c.name.Contains("-vs-")).All(c=>c.hurtFrames>5&&c.collisionRestored&&c.victimEnd.y<.05f));
        Check("dash-only-authored-sprinting-person-in-disabled-pressed-and-normal",impact.dashSymbols.Count>=16&&impact.dashSymbols.All(d=>d.overlayDisabled&&d.personVisible&&d.personParticleSuppressed
            &&(d.phase.Contains("disabled")?!d.interactable&&!d.held:d.phase.Contains("pressed")?d.interactable&&d.held:d.interactable&&!d.held)));
        var crowd=impact.cases.First(c=>c.name=="crowded-six");
        Check("accumulated-145-knockoff-still-hits-and-lands",impact.cases.First(c=>c.name=="145-accumulated-knockoff").frames.Any(f=>f.victimState=="KnockOff")&&impact.cases.First(c=>c.name=="145-accumulated-knockoff").victimEnd.y<.05f);
        Check("deep-overlap-separation-remains-bounded",impact.crowd.All(c=>c.maxStep<.2f&&c.maxSpeed<3f));
        Check("crowd-contact-settles-without-render-jitter",crowd.frames.Where(f=>f.time>3).All(f=>f.victimVelocity.magnitude<.15f));
        Check("all-six-crowded-bodies-settle-without-jitter-or-penetration",impact.crowd.Count==6&&impact.crowd.All(c=>c.settledFrames>10&&c.settledMaxStep<.02f&&c.settledMaxSpeed<.15f&&c.settledMaxPenetration<.04f));
        SaveImpact();
    }
    static void SaveImpact(){if(impact!=null){Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"impact.json"),JsonUtility.ToJson(impact,true));}}
}
