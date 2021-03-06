' 
' Copyright (C) 2011 SpuriousZero <http://www.spuriousemu.com/>
'
' This program is free software; you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation; either version 2 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY; without even the implied warranty of
' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
' GNU General Public License for more details.
'
' You should have received a copy of the GNU General Public License
' along with this program; if not, write to the Free Software
' Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
'

Imports System.Threading
Imports System.Collections.Generic
Imports SpuriousZero.Common.BaseWriter

Public Module WS_TimerBasedEvents

    Public Regenerator As TRegenerator
    Public AIManager As TAIManager
    Public SpellManager As TSpellManager
    Public CharacterSaver As TCharacterSaver
    Public WeatherChanger As TWeatherChanger

    'NOTE: Regenerates players' Mana, Life and Rage
    Public Class TRegenerator
        Implements IDisposable

        Private RegenerationTimer As Threading.Timer = Nothing
        Private RegenerationWorking As Boolean = False

        Private operationsCount As Integer
        Private BaseMana As Integer
        Private BaseLife As Integer
        Private BaseRage As Integer
        Private BaseEnergy As Integer
        Private _updateFlag As Boolean

        Private NextGroupUpdate As Boolean = True

        Public Const REGENERATION_TIMER As Integer = 2          'Timer period (sec)
        Public Const REGENERATION_ENERGY As Integer = 20        'Base energy regeneration rate
        Public Const REGENERATION_RAGE As Integer = 25          'Base rage degeneration rate (Rage = 1000 but shows only as 100 in game)
        Public Sub New()
            RegenerationTimer = New Threading.Timer(AddressOf Regenerate, Nothing, 10000, REGENERATION_TIMER * 1000)
        End Sub
        Private Sub Regenerate(ByVal state As Object)
            If RegenerationWorking Then
                Log.WriteLine(LogType.WARNING, "Update: Regenerator skipping update")
                Exit Sub
            End If

            RegenerationWorking = True
            NextGroupUpdate = Not NextGroupUpdate 'Group update = every 4 sec
            Try
                CHARACTERs_Lock.AcquireReaderLock(DEFAULT_LOCK_TIMEOUT)
                For Each Character As KeyValuePair(Of ULong, CharacterObject) In CHARACTERs
                    'DONE: If all invalid check passed then regenerate
                    'DONE: If dead don't regenerate
                    If (Not Character.Value.DEAD) AndAlso (Character.Value.underWaterTimer Is Nothing) AndAlso (Character.Value.LogoutTimer Is Nothing) AndAlso (Character.Value.Client IsNot Nothing) Then
                        With CType(Character.Value, CharacterObject)


                            BaseMana = .Mana.Current
                            BaseRage = .Rage.Current
                            BaseEnergy = .Energy.Current
                            BaseLife = .Life.Current
                            _updateFlag = False

                            'Rage
                            'DONE: In combat do not decrease, but send updates
                            If .ManaType = ManaTypes.TYPE_RAGE Then
                                If (.cUnitFlags And UnitFlags.UNIT_FLAG_IN_COMBAT) = 0 Then
                                    If .Rage.Current > 0 Then
                                        .Rage.Current -= REGENERATION_RAGE
                                    End If
                                ElseIf .RageRegenBonus <> 0 Then 'In Combat Regen from spells
                                    .Rage.Increment(.RageRegenBonus)
                                End If
                            End If

                            'Energy
                            If .ManaType = ManaTypes.TYPE_ENERGY AndAlso .Energy.Current < .Energy.Maximum Then
                                .Energy.Increment(REGENERATION_ENERGY)
                            End If

                            'Mana
                            If .ManaRegen = 0 Then .UpdateManaRegen()
                            'DONE: Don't regenerate while casting, 5 second rule
                            'TODO: If c.ManaRegenerationWhileCastingPercent > 0 ...
                            If .spellCastManaRegeneration = 0 Then
                                If (.ManaType = ManaTypes.TYPE_MANA OrElse .Classe = Classes.CLASS_DRUID) AndAlso .Mana.Current < .Mana.Maximum Then
                                    .Mana.Increment(.ManaRegen * REGENERATION_TIMER)
                                End If
                            Else
                                If (.ManaType = ManaTypes.TYPE_MANA OrElse .Classe = Classes.CLASS_DRUID) AndAlso .Mana.Current < .Mana.Maximum Then
                                    .Mana.Increment(.ManaRegenInterrupt * REGENERATION_TIMER)
                                End If
                                If .spellCastManaRegeneration < REGENERATION_TIMER Then
                                    .spellCastManaRegeneration = 0
                                Else
                                    .spellCastManaRegeneration -= REGENERATION_TIMER
                                End If
                            End If

                            'Life
                            'DONE: Don't regenerate in combat
                            'TODO: If c.LifeRegenWhileFightingPercent > 0 ...
                            If .Life.Current < .Life.Maximum AndAlso (.cUnitFlags And UnitFlags.UNIT_FLAG_IN_COMBAT) = 0 Then
                                Select Case .Classe
                                    Case Classes.CLASS_MAGE
                                        .Life.Increment(CType((.Spirit.Base * 0.1) * .LifeRegenerationModifier, Integer) + .LifeRegenBonus)
                                    Case Classes.CLASS_PRIEST
                                        .Life.Increment(CType((.Spirit.Base * 0.1) * .LifeRegenerationModifier, Integer) + .LifeRegenBonus)
                                    Case Classes.CLASS_WARLOCK
                                        .Life.Increment(CType((.Spirit.Base * 0.11) * .LifeRegenerationModifier, Integer) + .LifeRegenBonus)
                                    Case Classes.CLASS_DRUID
                                        .Life.Increment(CType((.Spirit.Base * 0.11) * .LifeRegenerationModifier, Integer) + .LifeRegenBonus)
                                    Case Classes.CLASS_SHAMAN
                                        .Life.Increment(CType((.Spirit.Base * 0.11) * .LifeRegenerationModifier, Integer) + .LifeRegenBonus)
                                    Case Classes.CLASS_ROGUE
                                        .Life.Increment(CType((.Spirit.Base * 0.5) * .LifeRegenerationModifier, Integer) + .LifeRegenBonus)
                                    Case Classes.CLASS_WARRIOR
                                        .Life.Increment(CType((.Spirit.Base * 0.8) * .LifeRegenerationModifier, Integer) + .LifeRegenBonus)
                                    Case Classes.CLASS_HUNTER
                                        .Life.Increment(CType((.Spirit.Base * 0.25) * .LifeRegenerationModifier, Integer) + .LifeRegenBonus)
                                    Case Classes.CLASS_PALADIN
                                        .Life.Increment(CType((.Spirit.Base * 0.25) * .LifeRegenerationModifier, Integer) + .LifeRegenBonus)
                                End Select
                            End If

                            'DONE: Send updates to players near
                            If BaseMana <> .Mana.Current Then
                                _updateFlag = True
                                .GroupUpdateFlag = .GroupUpdateFlag Or PartyMemberStatsFlag.GROUP_UPDATE_FLAG_CUR_POWER
                                .SetUpdateFlag(EUnitFields.UNIT_FIELD_POWER1, .Mana.Current)
                            End If
                            If BaseRage <> .Rage.Current Or ((.cUnitFlags And UnitFlags.UNIT_FLAG_IN_COMBAT) = UnitFlags.UNIT_FLAG_IN_COMBAT) Then
                                _updateFlag = True
                                .GroupUpdateFlag = .GroupUpdateFlag Or PartyMemberStatsFlag.GROUP_UPDATE_FLAG_CUR_POWER
                                .SetUpdateFlag(EUnitFields.UNIT_FIELD_POWER2, .Rage.Current)
                            End If
                            If BaseEnergy <> .Energy.Current Then
                                _updateFlag = True
                                .GroupUpdateFlag = .GroupUpdateFlag Or PartyMemberStatsFlag.GROUP_UPDATE_FLAG_CUR_POWER
                                .SetUpdateFlag(EUnitFields.UNIT_FIELD_POWER4, .Energy.Current)
                            End If
                            If BaseLife <> .Life.Current Then
                                _updateFlag = True
                                .SetUpdateFlag(EUnitFields.UNIT_FIELD_HEALTH, .Life.Current)
                                .GroupUpdateFlag = .GroupUpdateFlag Or PartyMemberStatsFlag.GROUP_UPDATE_FLAG_CUR_HP
                            End If

                            If _updateFlag Then .SendCharacterUpdate()


                            'DONE: Duel counter
                            If .DuelOutOfBounds <> DUEL_COUNTER_DISABLED Then
                                .DuelOutOfBounds -= REGENERATION_TIMER
                                If .DuelOutOfBounds = 0 Then DuelComplete(.DuelPartner, .Client.Character)
                            End If

                            'Check combat, incase of pvp action
                            .CheckCombat()

                            'Send group update
                            If NextGroupUpdate Then .GroupUpdate()

                            'Send UPDATE_OUT_OF_RANGE
                            If .guidsForRemoving.Count > 0 Then .SendOutOfRangeUpdate()
                        End With
                    End If
                Next

            Catch ex As Exception
                Log.WriteLine(LogType.WARNING, "Error at regenerate.{0}", vbNewLine & ex.ToString)
            Finally
                CHARACTERs_Lock.ReleaseReaderLock()
            End Try
            RegenerationWorking = False
        End Sub
        Public Sub Dispose() Implements System.IDisposable.Dispose
            RegenerationTimer.Dispose()
            RegenerationTimer = Nothing
        End Sub
    End Class


    'NOTE: Manages spell durations and DOT spells
    Public Class TSpellManager
        Implements IDisposable

        Private SpellManagerTimer As Threading.Timer = Nothing
        Private SpellManagerWorking As Boolean = False

        Public Const UPDATE_TIMER As Integer = 1000        'Timer period (ms)
        Public Sub New()
            SpellManagerTimer = New Threading.Timer(AddressOf Update, Nothing, 10000, UPDATE_TIMER)
        End Sub
        Private Sub Update(ByVal state As Object)
            If SpellManagerWorking Then
                Log.WriteLine(LogType.WARNING, "Update: Spell Manager skipping update")
                Exit Sub
            End If

            SpellManagerWorking = True

            Try

                WORLD_CREATUREs_Lock.AcquireReaderLock(DEFAULT_LOCK_TIMEOUT)

                For i As Long = 0 To WORLD_CREATUREsKeys.Count - 1
                    If WORLD_CREATUREs(WORLD_CREATUREsKeys(i)) IsNot Nothing Then
                        UpdateSpells(WORLD_CREATUREs(WORLD_CREATUREsKeys(i)))
                    End If
                Next

            Catch ex As Exception
                Log.WriteLine(LogType.FAILED, ex.ToString, Nothing)
            Finally
                WORLD_CREATUREs_Lock.ReleaseReaderLock()
            End Try

            Try
                CHARACTERs_Lock.AcquireReaderLock(DEFAULT_LOCK_TIMEOUT)
                For Each Character As KeyValuePair(Of ULong, CharacterObject) In CHARACTERs
                    If Character.Value IsNot Nothing Then UpdateSpells(Character.Value)
                Next
            Catch ex As Exception
                Log.WriteLine(LogType.FAILED, ex.ToString, Nothing)
            Finally
                CHARACTERs_Lock.ReleaseReaderLock()
            End Try

            Dim DynamicObjectsToDelete As New List(Of DynamicObjectObject)
            Try
                WORLD_DYNAMICOBJECTs_Lock.AcquireReaderLock(DEFAULT_LOCK_TIMEOUT)
                For Each Dynamic As KeyValuePair(Of ULong, DynamicObjectObject) In WORLD_DYNAMICOBJECTs
                    If Dynamic.Value IsNot Nothing AndAlso Dynamic.Value.Update() Then
                        DynamicObjectsToDelete.Add(Dynamic.Value)
                    End If
                Next
            Catch ex As Exception
                Log.WriteLine(LogType.FAILED, ex.ToString, Nothing)
            Finally
                WORLD_DYNAMICOBJECTs_Lock.ReleaseReaderLock()
            End Try

            For Each Dynamic As DynamicObjectObject In DynamicObjectsToDelete
                If Dynamic IsNot Nothing Then Dynamic.Delete()
            Next

            SpellManagerWorking = False
        End Sub
        Public Sub Dispose() Implements System.IDisposable.Dispose
            SpellManagerTimer.Dispose()
            SpellManagerTimer = Nothing
        End Sub

        Private Sub UpdateSpells(ByRef c As BaseUnit)

            If TypeOf c Is TotemObject Then
                CType(c, TotemObject).Update()
            Else
                For i As Integer = 0 To MAX_AURA_EFFECTs - 1
                    If c.ActiveSpells(i) IsNot Nothing Then

                        'DONE: Count aura duration
                        If c.ActiveSpells(i).SpellDuration <> SPELL_DURATION_INFINITE Then
                            c.ActiveSpells(i).SpellDuration -= UPDATE_TIMER

                            'DONE: Cast aura (check if: there is aura; aura is periodic; time for next activation)
                            For j As Byte = 0 To 2
                                If c.ActiveSpells(i) IsNot Nothing AndAlso c.ActiveSpells(i).Aura(j) IsNot Nothing AndAlso _
                                c.ActiveSpells(i).Aura_Info(j) IsNot Nothing AndAlso c.ActiveSpells(i).Aura_Info(j).Amplitude <> 0 AndAlso _
                                ((c.ActiveSpells(i).GetSpellInfo.GetDuration - c.ActiveSpells(i).SpellDuration) Mod c.ActiveSpells(i).Aura_Info(j).Amplitude) = 0 Then
                                    c.ActiveSpells(i).Aura(j).Invoke(c, c.ActiveSpells(i).SpellCaster, c.ActiveSpells(i).Aura_Info(j), c.ActiveSpells(i).SpellID, c.ActiveSpells(i).StackCount + 1, WS_Spells.AuraAction.AURA_UPDATE)
                                End If
                            Next j

                            'DONE: Remove finished aura
                            If c.ActiveSpells(i) IsNot Nothing AndAlso c.ActiveSpells(i).SpellDuration <= 0 AndAlso c.ActiveSpells(i).SpellDuration <> SPELL_DURATION_INFINITE Then c.RemoveAura(i, c.ActiveSpells(i).SpellCaster, True)
                        End If

                        'DONE: Check if there are units that are out of range for the area aura
                        For j As Byte = 0 To 2
                            If c.ActiveSpells(i) IsNot Nothing AndAlso c.ActiveSpells(i).Aura_Info(j) IsNot Nothing Then
                                If c.ActiveSpells(i).Aura_Info(j).ID = SpellEffects_Names.SPELL_EFFECT_APPLY_AREA_AURA Then
                                    If c.ActiveSpells(i).SpellCaster Is c Then
                                        'DONE: Check if there are friendly targets around you that does not have your aura
                                        Dim Targets As New List(Of BaseUnit)
                                        If TypeOf c Is CharacterObject Then
                                            Targets = GetPartyMembersAroundMe(CType(c, CharacterObject), c.ActiveSpells(i).Aura_Info(j).GetRadius)
                                        ElseIf (TypeOf c Is TotemObject) AndAlso CType(c, TotemObject).Caster IsNot Nothing AndAlso (TypeOf CType(c, TotemObject).Caster Is CharacterObject) Then
                                            Targets = GetPartyMembersAtPoint(CType(c, TotemObject).Caster, c.ActiveSpells(i).Aura_Info(j).GetRadius, c.positionX, c.positionY, c.positionZ)
                                        End If

                                        For Each Unit As BaseUnit In Targets
                                            If Unit.HaveAura(c.ActiveSpells(i).SpellID) = False Then
                                                ApplyAura(Unit, c, c.ActiveSpells(i).Aura_Info(j), c.ActiveSpells(i).SpellID)
                                            End If
                                        Next
                                    Else
                                        'DONE: Check if your aura source is too far away, has removed the aura or you / the source left the group
                                        If c.ActiveSpells(i).SpellCaster IsNot Nothing AndAlso c.ActiveSpells(i).SpellCaster.Exist Then
                                            Dim caster As CharacterObject = Nothing
                                            If TypeOf c.ActiveSpells(i).SpellCaster Is CharacterObject Then
                                                caster = CType(c.ActiveSpells(i).SpellCaster, CharacterObject)
                                            ElseIf (TypeOf c.ActiveSpells(i).SpellCaster Is TotemObject) AndAlso CType(c.ActiveSpells(i).SpellCaster, TotemObject).Caster IsNot Nothing AndAlso (TypeOf CType(c.ActiveSpells(i).SpellCaster, TotemObject).Caster Is CharacterObject) Then
                                                caster = CType(CType(c.ActiveSpells(i).SpellCaster, TotemObject).Caster, CharacterObject)
                                            End If

                                            If caster Is Nothing OrElse caster.Group Is Nothing OrElse caster.Group.LocalMembers.Contains(c.GUID) = False Then
                                                c.RemoveAura(i, c.ActiveSpells(i).SpellCaster)
                                            Else
                                                If c.ActiveSpells(i).SpellCaster.HaveAura(c.ActiveSpells(i).SpellID) = False Then
                                                    c.RemoveAura(i, c.ActiveSpells(i).SpellCaster)
                                                Else
                                                    If GetDistance(c, c.ActiveSpells(i).SpellCaster) > c.ActiveSpells(i).Aura_Info(j).GetRadius Then
                                                        c.RemoveAura(i, c.ActiveSpells(i).SpellCaster)
                                                    End If
                                                End If
                                            End If
                                        Else
                                            c.RemoveAura(i, c.ActiveSpells(i).SpellCaster)
                                        End If
                                    End If
                                End If
                            End If
                        Next

                    End If
                Next
            End If

        End Sub
    End Class


    'NOTE: Manages ai movement
    Public Class TAIManager
        Implements IDisposable

        Public AIManagerTimer As Threading.Timer = Nothing
        Private AIManagerWorking As Boolean = False

        Public Const UPDATE_TIMER As Integer = 1000     'Timer period (ms)
        Public Sub New()
            AIManagerTimer = New Threading.Timer(AddressOf Update, Nothing, 10000, UPDATE_TIMER)
        End Sub
        Private Sub Update(ByVal state As Object)
            If AIManagerWorking Then
                Log.WriteLine(LogType.WARNING, "Update: AI Manager skipping update")
                Exit Sub
            End If

            Dim StartTime As Integer = timeGetTime
            AIManagerWorking = True

            'First transports
            Try
                WORLD_TRANSPORTs_Lock.AcquireReaderLock(DEFAULT_LOCK_TIMEOUT)

                For Each Transport As KeyValuePair(Of ULong, TransportObject) In WORLD_TRANSPORTs
                    Transport.Value.Update()
                Next

            Catch ex As Exception
                Log.WriteLine(LogType.CRITICAL, "Error updating transports.{0}{1}", vbNewLine, ex.ToString)
            Finally
                WORLD_TRANSPORTs_Lock.ReleaseReaderLock()
            End Try

            'Then creatures
            Try
                WORLD_CREATUREs_Lock.AcquireReaderLock(DEFAULT_LOCK_TIMEOUT)

                Try
                    For i As Long = 0 To WORLD_CREATUREsKeys.Count - 1
                        If WORLD_CREATUREs(WORLD_CREATUREsKeys(i)) IsNot Nothing AndAlso WORLD_CREATUREs(WORLD_CREATUREsKeys(i)).aiScript IsNot Nothing Then
                            WORLD_CREATUREs(WORLD_CREATUREsKeys(i)).aiScript.DoThink()
                        End If
                    Next
                Catch ex As Exception
                    Log.WriteLine(LogType.CRITICAL, "Error updating AI.{0}{1}", vbNewLine, ex.ToString)
                Finally
                    WORLD_CREATUREs_Lock.ReleaseReaderLock()
                End Try

            Catch ex As ApplicationException
                Log.WriteLine(LogType.WARNING, "Update: AI Manager timed out")
            Catch ex As Exception
                Log.WriteLine(LogType.CRITICAL, "Error updating AI.{0}{1}", vbNewLine, ex.ToString)
            End Try
            AIManagerWorking = False
        End Sub
        Public Sub Dispose() Implements System.IDisposable.Dispose
            AIManagerTimer.Dispose()
            AIManagerTimer = Nothing
        End Sub

        Protected Overrides Sub Finalize()
            MyBase.Finalize()
        End Sub
    End Class


    'NOTE: Manages character savings
    Public Class TCharacterSaver
        Implements IDisposable

        Public CharacterSaverTimer As Threading.Timer = Nothing
        Private CharacterSaverWorking As Boolean = False

        Public UPDATE_TIMER As Integer = Config.SaveTimer     'Timer period (ms)
        Public Sub New()
            CharacterSaverTimer = New Threading.Timer(AddressOf Update, Nothing, 10000, UPDATE_TIMER)
        End Sub
        Private Sub Update(ByVal state As Object)
            If CharacterSaverWorking Then
                Log.WriteLine(LogType.WARNING, "Update: Character Saver skipping update")
                Exit Sub
            End If

            CharacterSaverWorking = True
            Try
                CHARACTERs_Lock.AcquireReaderLock(DEFAULT_LOCK_TIMEOUT)
                For Each Character As KeyValuePair(Of ULong, CharacterObject) In CHARACTERs
                    Character.Value.SaveCharacter()
                Next
            Catch ex As Exception
                Log.WriteLine(LogType.FAILED, ex.ToString, Nothing)
            Finally
                CHARACTERs_Lock.ReleaseReaderLock()
            End Try

            'Here we hook the instance expire checks too
            InstanceMapUpdate()

            CharacterSaverWorking = False
        End Sub
        Public Sub Dispose() Implements System.IDisposable.Dispose
            CharacterSaverTimer.Dispose()
            CharacterSaverTimer = Nothing
        End Sub
    End Class


    'NOTE: Manages the weather
    Public Class TWeatherChanger
        Implements IDisposable

        Public WeatherTimer As Threading.Timer = Nothing
        Private WeatherWorking As Boolean = False

        Public UPDATE_TIMER As Integer = Config.WeatherTimer     'Timer period (ms)
        Public Sub New()
            WeatherTimer = New Threading.Timer(AddressOf Update, Nothing, 10000, UPDATE_TIMER)
        End Sub
        Private Sub Update(ByVal state As Object)
            If WeatherWorking Then
                Log.WriteLine(LogType.WARNING, "Update: Weather changer skipping update")
                Exit Sub
            End If

            WeatherWorking = True

            For Each Weather As KeyValuePair(Of Integer, WeatherZone) In WeatherZones
                Weather.Value.Update()
            Next

            WeatherWorking = False
        End Sub
        Public Sub Dispose() Implements System.IDisposable.Dispose
            WeatherTimer.Dispose()
            WeatherTimer = Nothing
        End Sub
    End Class

    'TODO: Timer for kicking not connected players (ping timeout)
    'TODO: Timer for auction items and mails
    'TODO: Timer for weather change

End Module


