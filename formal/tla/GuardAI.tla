------------------------------ MODULE GuardAI ------------------------------
(***************************************************************************)
(* Formal TLA+ Specification & Model-Checked Verification of the           *)
(* Frischemarkt / Dither's House Dual-Architecture NPC Guard System        *)
(* (61CSE327 Project 2):                                                   *)
(*   1. 7-State Table-Driven Finite State Machine (GuardFsm.cs)            *)
(*   2. 1-Token Place/Transition Petri Net Marking Invariant               *)
(*   3. Reactive Priority Behavior Tree with Preemption (GuardBt.cs)       *)
(*   4. Stuttering Bisimulation & Exact 1-Tick Attack Preemption Theorem   *)
(*   5. Temporal Liveness (Eventual Return to Patrol under Quiet Env)      *)
(***************************************************************************)
EXTENDS Integers, FiniteSets

States == {"idle", "patrol", "investigate", "chase", "attack", "search", "return"}

VARIABLES
    fsmState,       \* Current 7-state FSM state
    btLatch,        \* Reactive BT blackboard LatchState
    btSingleStep,   \* Reactive BT evaluated from the same pre-state as FSM
    marking,        \* 1-Token Petri Net place marking: [States -> {0, 1}]
    abortedNode,    \* Node preempted via .Abort() on this tick ("none" or state name)
    \* Sensory percepts sampled at the current tick:
    playerVisible,
    inAttackRange,
    heardSound,
    arrivedAndQuiet,
    lostSightLong,
    searchExpired,
    atRouteStart,
    \* Quiet-environment progress phase for temporal liveness verification:
    quietPhase

vars == <<fsmState, btLatch, btSingleStep, marking, abortedNode,
          playerVisible, inAttackRange, heardSound,
          arrivedAndQuiet, lostSightLong, searchExpired, atRouteStart,
          quietPhase>>

-----------------------------------------------------------------------------
(* Priority-Ordered 7-State FSM Transition Function (mirrors GuardFsm.cs)  *)
-----------------------------------------------------------------------------
FsmNextState(s, vis, atk, snd, arr, lost, exp, start) ==
    IF s = "idle" THEN
        IF vis THEN "chase"
        ELSE IF snd THEN "investigate"
        ELSE "patrol"
    ELSE IF s = "patrol" THEN
        IF vis THEN "chase"
        ELSE IF snd THEN "investigate"
        ELSE "patrol"
    ELSE IF s = "investigate" THEN
        IF vis THEN "chase"
        ELSE IF arr /\ ~vis THEN "search"
        ELSE "investigate"
    ELSE IF s = "chase" THEN
        IF vis /\ atk THEN "attack"
        ELSE IF ~vis /\ lost THEN "search"
        ELSE "chase"
    ELSE IF s = "attack" THEN
        IF (vis /\ ~atk) \/ (~vis /\ ~lost) THEN "chase"
        ELSE IF ~vis /\ lost THEN "search"
        ELSE "attack"
    ELSE IF s = "search" THEN
        IF vis THEN "chase"
        ELSE IF snd THEN "investigate"
        ELSE IF ~vis /\ exp THEN "return"
        ELSE "search"
    ELSE (* s = "return" *)
        IF vis THEN "chase"
        ELSE IF snd THEN "investigate"
        ELSE IF start THEN "patrol"
        ELSE "return"

-----------------------------------------------------------------------------
(* Reactive Priority Behavior Tree Evaluation (mirrors GuardBt.cs)         *)
(* Root ReactiveSelector priority order:                                   *)
(*   1. Engage: Attack (if vis /\ atk) else Chase (if vis or latched)      *)
(*   2. Investigate: if snd or (latched investigate /\ ~arr)               *)
(*   3. Search: if (latched chase/attack /\ lost) or (latched inv /\ arr)  *)
(*              or (latched search /\ ~exp)                                *)
(*   4. Return: if (latched search /\ exp) or (latched return /\ ~start)   *)
(*   5. Patrol: fallback default                                           *)
-----------------------------------------------------------------------------
ShouldChase(latch, vis, lost) ==
    vis \/ ((latch \in {"chase", "attack"}) /\ ~lost)

ShouldInvestigate(latch, snd, arr, vis) ==
    snd \/ (latch = "investigate" /\ ~(arr /\ ~vis))

ShouldSearch(latch, lost, arr, vis, exp) ==
    ((latch \in {"chase", "attack"}) /\ (~vis /\ lost))
    \/ (latch = "investigate" /\ (arr /\ ~vis))
    \/ (latch = "search" /\ ~(~vis /\ exp))

ShouldReturn(latch, exp, vis, start) ==
    (latch = "search" /\ (~vis /\ exp))
    \/ (latch = "return" /\ ~start)

BtNextLeaf(latch, vis, atk, snd, arr, lost, exp, start) ==
    IF vis /\ atk THEN "attack"
    ELSE IF ShouldChase(latch, vis, lost) THEN "chase"
    ELSE IF ShouldInvestigate(latch, snd, arr, vis) THEN "investigate"
    ELSE IF ShouldSearch(latch, lost, arr, vis, exp) THEN "search"
    ELSE IF ShouldReturn(latch, exp, vis, start) THEN "return"
    ELSE "patrol"

-----------------------------------------------------------------------------
(* 1-Token Place/Transition Petri Net FiringUpdate                         *)
(* Firing transition t: src -> dst consumes 1 token from src and deposits  *)
(* 1 token into dst: M' = M - e_src + e_dst.                               *)
-----------------------------------------------------------------------------
FirePetriNet(m, src, dst) ==
    [s \in States |->
        IF s = dst THEN
            (IF s = src THEN m[s] ELSE m[s] + 1)
        ELSE IF s = src THEN m[s] - 1
        ELSE m[s]]

SumMarking(m) ==
    m["idle"] + m["patrol"] + m["investigate"] + m["chase"] +
    m["attack"] + m["search"] + m["return"]

-----------------------------------------------------------------------------
(* Initial State & Transitions                                             *)
-----------------------------------------------------------------------------
Init ==
    /\ fsmState = "patrol"
    /\ btLatch = "patrol"
    /\ btSingleStep = "patrol"
    /\ marking = [s \in States |-> IF s = "patrol" THEN 1 ELSE 0]
    /\ abortedNode = "none"
    /\ playerVisible = FALSE
    /\ inAttackRange = FALSE
    /\ heardSound = FALSE
    /\ arrivedAndQuiet = FALSE
    /\ lostSightLong = FALSE
    /\ searchExpired = FALSE
    /\ atRouteStart = TRUE
    /\ quietPhase = 0

(* Environment non-deterministically samples consistent sensor percepts,   *)
(* and both controllers tick synchronously on the same percept tuple.      *)
EnvTick ==
    \E vis \in BOOLEAN, atk \in BOOLEAN, snd \in BOOLEAN,
       arr \in BOOLEAN, lost \in BOOLEAN, exp \in BOOLEAN, start \in BOOLEAN :
        \* Physical consistency constraints of GuardPerception.cs & HeistGame.cs:
        /\ (atk => vis)          \* in_attack_range requires player_visible
        /\ (lost => ~vis)        \* lost_sight_long only holds while unseen
        /\ (arr => (~vis /\ ~snd)) \* arrived_and_quiet requires quiet arrival
        /\ (exp => (~vis /\ ~snd)) \* search_expired requires quiet search
        /\ LET nextFsm  == FsmNextState(fsmState, vis, atk, snd, arr, lost, exp, start)
               nextBt   == BtNextLeaf(btLatch, vis, atk, snd, arr, lost, exp, start)
               stepBt   == BtNextLeaf(fsmState, vis, atk, snd, arr, lost, exp, start)
           IN
           /\ fsmState' = nextFsm
           /\ btLatch' = nextBt
           /\ btSingleStep' = stepBt
           /\ marking' = FirePetriNet(marking, fsmState, nextFsm)
           /\ abortedNode' = IF btLatch /= nextBt THEN btLatch ELSE "none"
           /\ playerVisible' = vis
           /\ inAttackRange' = atk
           /\ heardSound' = snd
           /\ arrivedAndQuiet' = arr
           /\ lostSightLong' = lost
           /\ searchExpired' = exp
           /\ atRouteStart' = start
           /\ quietPhase' = 0

(* Quiet progression action: when the thief hides (~vis /\ ~snd), sensor   *)
(* timers advance monotonically through lost_sight -> search_expired ->    *)
(* at_route_start -> patrol.                                               *)
QuietStep ==
    LET vis   == FALSE
        atk   == FALSE
        snd   == FALSE
        lost  == (fsmState \in {"chase", "attack"} \/ btLatch \in {"chase", "attack"})
        arr   == (fsmState = "investigate" \/ btLatch = "investigate")
        exp   == (fsmState = "search" \/ btLatch = "search")
        start == (fsmState = "return" \/ btLatch = "return" \/ fsmState = "patrol")
        nextFsm == FsmNextState(fsmState, vis, atk, snd, arr, lost, exp, start)
        nextBt  == BtNextLeaf(btLatch, vis, atk, snd, arr, lost, exp, start)
        stepBt  == BtNextLeaf(fsmState, vis, atk, snd, arr, lost, exp, start)
    IN
    /\ fsmState' = nextFsm
    /\ btLatch' = nextBt
    /\ btSingleStep' = stepBt
    /\ marking' = FirePetriNet(marking, fsmState, nextFsm)
    /\ abortedNode' = IF btLatch /= nextBt THEN btLatch ELSE "none"
    /\ playerVisible' = vis
    /\ inAttackRange' = atk
    /\ heardSound' = snd
    /\ arrivedAndQuiet' = arr
    /\ lostSightLong' = lost
    /\ searchExpired' = exp
    /\ atRouteStart' = start
    /\ quietPhase' = IF nextFsm = "patrol" /\ nextBt = "patrol" THEN 0
                     ELSE IF quietPhase < 4 THEN quietPhase + 1 ELSE 4

Next == EnvTick \/ QuietStep

\* Weak fairness on QuietStep verifies that whenever the thief stops making
\* noise and breaks LOS, the guard is guaranteed to return to Patrol.
Spec == Init /\ [][Next]_vars /\ WF_vars(QuietStep)

-----------------------------------------------------------------------------
(* Safety Invariants & Observational Equivalence Theorems                  *)
-----------------------------------------------------------------------------

TypeOK ==
    /\ fsmState \in States
    /\ btLatch \in States
    /\ btSingleStep \in States
    /\ marking \in [States -> {0, 1}]
    /\ abortedNode \in (States \cup {"none"})

(* Theorem 1: 1-Token Place/Transition Petri Net Mutual Exclusion          *)
(* At every reachable state, the total token count across all 7 places is  *)
(* strictly 1, and that unique token resides in fsmState.                  *)
OneTokenPetriInvariant ==
    /\ SumMarking(marking) = 1
    /\ marking[fsmState] = 1
    /\ \A s \in States : (s /= fsmState) => (marking[s] = 0)

(* Theorem 2: Exact Single-Step Stuttering Bisimulation Theorem            *)
(* Evaluated from the same pre-state and consistent percept tuple,         *)
(* btSingleStep and fsmState agree everywhere EXCEPT at the two 1-tick     *)
(* hierarchical priority-preemption boundaries:                            *)
(*   (a) Combat entry (playerVisible /\ inAttackRange):                    *)
(*       btSingleStep = "attack" /\ fsmState = "chase"                     *)
(*   (b) Acoustic re-acquisition on sight loss (lostSightLong /\ heardSound): *)
(*       btSingleStep = "investigate" /\ fsmState = "search"               *)
StutteringBisimulationInvariant ==
    (fsmState /= btSingleStep) =>
        \/ (btSingleStep = "attack" /\ fsmState = "chase" /\ playerVisible /\ inAttackRange)
        \/ (btSingleStep = "investigate" /\ fsmState = "search" /\ lostSightLong /\ heardSound)

(* Theorem 3: Temporal Liveness — Eventual Return to Patrol                *)
(* Whenever the thief remains hidden and quiet from some point onward      *)
(* (<>[](~playerVisible /\ ~heardSound)), both FSM and BT are guaranteed   *)
(* to eventually return to and remain in "patrol" (zero deadlocks).        *)
EventualPatrolLiveness ==
    (<>[](~playerVisible /\ ~heardSound)) => <>(fsmState = "patrol" /\ btLatch = "patrol")

=============================================================================
