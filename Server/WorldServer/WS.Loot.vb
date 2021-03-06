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
Imports SpuriousZero.Common.BaseWriter


Public Module WS_Loot

    Public LootTemplates_Creature As LootStore 'DONE!
    Public LootTemplates_Disenchant As LootStore
    Public LootTemplates_Fishing As LootStore 'DONE!
    Public LootTemplates_Gameobject As LootStore 'DONE!
    Public LootTemplates_Item As LootStore 'DONE!
    Public LootTemplates_Pickpocketing As LootStore 'DONE!
    Public LootTemplates_QuestMail As LootStore
    Public LootTemplates_Reference As LootStore 'DONE!
    Public LootTemplates_Skinning As LootStore 'DONE!

    Public LootTable As New Dictionary(Of ULong, LootObject)
    Public Enum LootType As Byte
        LOOTTYPE_CORPSE = 1
        LOOTTYPE_PICKPOCKETING = 2
        LOOTTYPE_FISHING = 3
        LOOTTYPE_DISENCHANTING = 4
        LOOTTYPE_SKINNING = 6
    End Enum

    Private Enum LootState As UInteger
        NORMAL = 0
        PASSING = 1
    End Enum

    Public Enum LockKeyType As Byte
        LOCK_KEY_NONE = 0
        LOCK_KEY_ITEM = 1
        LOCK_KEY_SKILL = 2
    End Enum

    Public Enum LockType As Byte
        LOCKTYPE_PICKLOCK = 1
        LOCKTYPE_HERBALISM = 2
        LOCKTYPE_MINING = 3
        LOCKTYPE_DISARM_TRAP = 4
        LOCKTYPE_OPEN = 5
        LOCKTYPE_TREASURE = 6
        LOCKTYPE_CALCIFIED_ELVEN_GEMS = 7
        LOCKTYPE_CLOSE = 8
        LOCKTYPE_ARM_TRAP = 9
        LOCKTYPE_QUICK_OPEN = 10
        LOCKTYPE_QUICK_CLOSE = 11
        LOCKTYPE_OPEN_TINKERING = 12
        LOCKTYPE_OPEN_KNEELING = 13
        LOCKTYPE_OPEN_ATTACKING = 14
        LOCKTYPE_GAHZRIDIAN = 15
        LOCKTYPE_BLASTING = 16
        LOCKTYPE_SLOW_OPEN = 17
        LOCKTYPE_SLOW_CLOSE = 18
        LOCKTYPE_FISHING = 19
    End Enum

    Public Enum ConditionType                   ' value1       value2  for the Condition enumed
        CONDITION_NONE = 0                      ' 0            0
        CONDITION_AURA = 1                      ' spell_id     effindex
        CONDITION_ITEM = 2                      ' item_id      count
        CONDITION_ITEM_EQUIPPED = 3             ' item_id      0
        CONDITION_ZONEID = 4                    ' zone_id      0
        CONDITION_REPUTATION_RANK = 5           ' faction_id   min_rank
        CONDITION_TEAM = 6                      ' player_team  0,      (469 - Alliance 67 - Horde)
        CONDITION_SKILL = 7                     ' skill_id     skill_value
        CONDITION_QUESTREWARDED = 8             ' quest_id     0
        CONDITION_QUESTTAKEN = 9                ' quest_id     0,      for condition true while quest active.
        CONDITION_AD_COMMISSION_AURA = 10       ' 0            0,      for condition true while one from AD ˝ommission aura active
        CONDITION_NO_AURA = 11                  ' spell_id     effindex
        CONDITION_ACTIVE_EVENT = 12             ' event_id
        CONDITION_INSTANCE_DATA = 13            ' entry        data
    End Enum

    Public Locks As New Dictionary(Of Integer, TLock)
    Public Class TLock
        Public KeyType(4) As Byte
        Public Keys(4) As Integer
        Public RequiredMiningSkill As Short
        Public RequiredLockingSkill As Short

        Public Sub New(ByVal KeyType_() As Byte, ByVal Keys_() As Integer, ByVal ReqMining As Short, ByVal ReqLock As Short)
            For i As Byte = 0 To 4
                KeyType(i) = KeyType_(i)
                Keys(i) = Keys_(i)
            Next
            RequiredMiningSkill = ReqMining
            RequiredLockingSkill = ReqLock
        End Sub
    End Class

#Region "LootItem"
    Public Class LootItem
        Implements IDisposable

        Public ItemID As Integer = 0
        Public ItemCount As Byte = 0

        Public ReadOnly Property ItemModel() As Integer
            Get
                If Not ITEMDatabase.ContainsKey(ItemID) Then Dim tmpItem As New ItemInfo(ItemID)
                Return ITEMDatabase(ItemID).Model
            End Get
        End Property

        Public Sub New(ByRef Item As LootStoreItem)
            Me.ItemID = Item.ItemID
            Me.ItemCount = CByte(Rnd.Next(Item.MinCountOrRef, Item.MaxCount + 1))
        End Sub

        Public Sub Dispose() Implements System.IDisposable.Dispose

        End Sub
    End Class
#End Region
#Region "LootStoreItem"
    Public Class LootStoreItem
        Public ItemID As Integer = 0
        Public Chance As Single = 0.0F
        Public Group As Byte = 0
        Public MinCountOrRef As Integer = 0
        Public MaxCount As Byte = 0
        Public LootCondition As ConditionType = ConditionType.CONDITION_NONE
        Public ConditionValue1 As Integer = 0
        Public ConditionValue2 As Integer = 0
        Public NeedQuest As Boolean = False

        Public Sub New(ByVal Item As Integer, ByVal Chance As Single, ByVal Group As Byte, ByVal MinCountOrRef As Integer, ByVal MaxCount As Byte, ByVal LootCondition As ConditionType, ByVal ConditionValue1 As Integer, ByVal ConditionValue2 As Integer, ByVal NeedQuest As Boolean)
            Me.ItemID = Item
            Me.Chance = Chance
            Me.Group = Group
            Me.MinCountOrRef = MinCountOrRef
            Me.MaxCount = MaxCount
            Me.LootCondition = LootCondition
            Me.ConditionValue1 = ConditionValue1
            Me.ConditionValue2 = ConditionValue2
            Me.NeedQuest = NeedQuest
        End Sub

        Public Function Roll() As Boolean
            If Chance >= 100.0F Then Return True
            Return RollChance(Chance)
        End Function

    End Class
#End Region
#Region "LootGroup"
    Public Class LootGroup
        Public ExplicitlyChanced As New List(Of LootStoreItem)
        Public EqualChanced As New List(Of LootStoreItem)

        Public Sub New()

        End Sub

        Public Sub AddItem(ByRef Item As LootStoreItem)
            If Item.Chance <> 0.0F Then
                ExplicitlyChanced.Add(Item)
            Else
                EqualChanced.Add(Item)
            End If
        End Sub

        Public Function Roll() As LootStoreItem
            If ExplicitlyChanced.Count > 0 Then
                Dim rollChance As Single = Rnd.NextDouble() * 100.0F

                For i As Integer = 0 To ExplicitlyChanced.Count - 1
                    If ExplicitlyChanced(i).Chance >= 100.0F Then Return ExplicitlyChanced(i)

                    rollChance -= ExplicitlyChanced(i).Chance
                    If rollChance <= 0.0F Then Return ExplicitlyChanced(i)
                Next
            End If
            If EqualChanced.Count > 0 Then
                Return EqualChanced(Rnd.Next(0, EqualChanced.Count))
            End If
            Return Nothing
        End Function

        Public Sub Process(ByRef Loot As LootObject)
            Dim Item As LootStoreItem = Roll()
            If Item IsNot Nothing Then Loot.Items.Add(New LootItem(Item))
        End Sub

    End Class
#End Region
#Region "LootObject"
    Public Class LootObject
        Implements IDisposable
        Public GUID As ULong = 0
        Public Items As New List(Of lootitem)
        Public Money As Integer = 0
        Public LootType As LootType = LootType.LOOTTYPE_CORPSE
        Public LootOwner As ULong = 0

        Public GroupLootInfo As New Dictionary(Of Integer, GroupLootInfo)(0)

        Public Sub New(ByVal GUID_ As ULong, ByVal LootType_ As LootType)
            LootTable(GUID_) = Me
            LootType = LootType_
            GUID = GUID_
        End Sub

        Public Sub SendLoot(ByRef Client As ClientClass)
            If Items.Count = 0 Then
                SendEmptyLoot(GUID, LootType, Client)
                Exit Sub
            End If
            If LootOwner <> 0 AndAlso Client.Character.GUID <> LootOwner Then
                'DONE: Loot owning!
                Dim notMy As New PacketClass(OPCODES.SMSG_INVENTORY_CHANGE_FAILURE)
                notMy.AddInt8(InventoryChangeFailure.EQUIP_ERR_OBJECT_IS_BUSY)
                notMy.AddUInt64(0)
                notMy.AddUInt64(0)
                notMy.AddInt8(0)
                Client.Send(notMy)
                notMy.Dispose()
                Exit Sub
            End If

            Dim response As New PacketClass(OPCODES.SMSG_LOOT_RESPONSE)
            response.AddUInt64(GUID)
            response.AddInt8(LootType)
            response.AddInt32(Money)
            response.AddInt8(Items.Count)

            Dim i As Byte
            For i = 0 To Items.Count - 1
                If Items(i) Is Nothing Then
                    response.AddInt8(i)
                    response.AddInt32(0)
                    response.AddInt32(0)
                    response.AddInt32(0)
                    response.AddUInt64(0)
                    response.AddInt8(0)
                Else
                    response.AddInt8(i)
                    response.AddInt32(Items(i).ItemID)
                    response.AddInt32(Items(i).ItemCount)
                    response.AddInt32(Items(i).ItemModel)
                    response.AddUInt64(0)
                    If Client.Character.IsInGroup AndAlso Client.Character.Group.LootMethod = GroupLootMethod.LOOT_MASTER AndAlso Client.Character.Group.LocalLootMaster IsNot Nothing AndAlso Client.Character.Group.LocalLootMaster IsNot Client.Character Then
                        response.AddInt8(2) 'Unlootable?
                    Else
                        response.AddInt8(0) '1: Message "Still rolled for."
                    End If
                End If
            Next

            Client.Send(response)
            response.Dispose()

            Client.Character.lootGUID = GUID

            If Client.Character.IsInGroup Then
                If Client.Character.Group.LootMethod = GroupLootMethod.LOOT_NEED_BEFORE_GREED Or Client.Character.Group.LootMethod = GroupLootMethod.LOOT_GROUP Then

                    'DONE: Check threshold if in group
                    For i = 0 To Items.Count - 1
                        If Not Items(i) Is Nothing Then
                            If CType(ITEMDatabase(Items(i).ItemID), ItemInfo).Quality >= Client.Character.Group.LootThreshold Then
                                GroupLootInfo(i) = New GroupLootInfo
                                CType(GroupLootInfo(i), GroupLootInfo).LootObject = Me
                                CType(GroupLootInfo(i), GroupLootInfo).LootSlot = i

                                CType(GroupLootInfo(i), GroupLootInfo).Item = Items(i)

                                StartRoll(GUID, i, Client.Character)
                                Exit Sub
                            End If
                        End If
                    Next

                End If
            End If
        End Sub

        Public Sub GetLoot(ByRef Client As ClientClass, ByVal Slot As Byte)
            Try
                If Items(Slot) Is Nothing Then
                    Dim response As New PacketClass(OPCODES.SMSG_INVENTORY_CHANGE_FAILURE)
                    response.AddInt8(InventoryChangeFailure.EQUIP_ERR_ALREADY_LOOTED)
                    response.AddUInt64(0)
                    response.AddUInt64(0)
                    response.AddInt8(0)
                    Client.Send(response)
                    response.Dispose()
                    Exit Sub
                End If
                If GroupLootInfo.ContainsKey(Slot) Then
                    Dim response As New PacketClass(OPCODES.SMSG_INVENTORY_CHANGE_FAILURE)
                    response.AddInt8(InventoryChangeFailure.EQUIP_ERR_OBJECT_IS_BUSY)
                    response.AddUInt64(0)
                    response.AddUInt64(0)
                    response.AddInt8(0)
                    Client.Send(response)
                    response.Dispose()
                    Exit Sub
                End If

                Dim tmpItem As New ItemObject(Items(Slot).ItemID, Client.Character.GUID)
                tmpItem.StackCount = Items(Slot).ItemCount

                If Client.Character.ItemADD(tmpItem) Then
                    'DONE: Bind item to player
                    If tmpItem.ItemInfo.Bonding = ITEM_BONDING_TYPE.BIND_WHEN_PICKED_UP Then tmpItem.SoulbindItem()

                    'TODO: If other players is looting the same object remove it for them as well.

                    Dim response As New PacketClass(OPCODES.SMSG_LOOT_REMOVED)
                    response.AddInt8(Slot)
                    Client.Send(response)
                    response.Dispose()

                    Client.Character.LogLootItem(tmpItem, Items(Slot).ItemCount, False, False)

                    Items(Slot).Dispose()
                    Items(Slot) = Nothing

                    If LootType = WS_Loot.LootType.LOOTTYPE_FISHING AndAlso IsEmpty Then
                        SendRelease(Client)
                        Me.Dispose()
                    End If
                Else
                    tmpItem.Delete()

                    Dim response As New PacketClass(OPCODES.SMSG_INVENTORY_CHANGE_FAILURE)
                    response.AddInt8(InventoryChangeFailure.EQUIP_ERR_INVENTORY_FULL)
                    response.AddUInt64(0)
                    response.AddUInt64(0)
                    response.AddInt8(0)
                    Client.Send(response)
                    response.Dispose()
                End If
            Catch e As Exception
                Log.WriteLine(LogType.DEBUG, "Error getting loot.{0}", vbNewLine & e.ToString)
            End Try
        End Sub

        Public Sub SendRelease(ByRef Client As ClientClass)
            Dim responseRelease As New PacketClass(OPCODES.SMSG_LOOT_RELEASE_RESPONSE)
            responseRelease.AddUInt64(GUID)
            responseRelease.AddInt8(1)
            Client.Send(responseRelease)
            responseRelease.Dispose()
        End Sub

        Public ReadOnly Property IsEmpty() As Boolean
            Get
                If Money <> 0 Then Return False
                For i As Integer = 0 To Items.Count - 1
                    If Items(i) IsNot Nothing Then Return False
                Next
                Return True
                'Return ((Items.Count = 0) And (Money = 0))
            End Get
        End Property

        Public Sub Dispose() Implements System.IDisposable.Dispose
            LootTable.Remove(GUID)
            Log.WriteLine(LogType.DEBUG, "Loot destroyed.")
        End Sub
    End Class
#End Region
#Region "LootTemplate"
    Public Class LootTemplate

        Public Items As New List(Of LootStoreItem)
        Public Groups As New Dictionary(Of Byte, LootGroup)

        Public Sub New()

        End Sub

        Public Sub AddItem(ByRef Item As LootStoreItem)
            If Item.Group > 0 AndAlso Item.MinCountOrRef > 0 Then
                If Groups.ContainsKey(Item.Group) = False Then Groups.Add(Item.Group, New LootGroup())
                Groups(Item.Group).AddItem(Item)
            Else
                Items.Add(Item)
            End If
        End Sub

        Public Sub Process(ByRef Loot As LootObject, ByVal GroupID As Byte)
            If GroupID > 0 Then
                If Groups.ContainsKey(GroupID) = False Then Exit Sub
                Groups(GroupID).Process(Loot)
                Exit Sub
            End If

            'Go through all items
            For i As Integer = 0 To Items.Count - 1
                If Items(i).Roll = False Then Continue For 'Bad luck

                If Items(i).MinCountOrRef < 0 Then 'Loot Template ID
                    Dim Referenced As LootTemplate = LootTemplates_Reference.GetLoot(-Items(i).MinCountOrRef)
                    If Referenced Is Nothing Then Continue For

                    For j As Integer = 1 To Items(i).MaxCount
                        Referenced.Process(Loot, Items(i).Group)
                    Next
                Else 'Normal Item
                    Loot.Items.Add(New LootItem(Items(i)))
                End If
            Next

            'Go through all loot groups
            For Each Group As KeyValuePair(Of Byte, LootGroup) In Groups
                Group.Value.Process(Loot)
            Next
        End Sub
    End Class
#End Region
#Region "LootStore"
    Public Class LootStore

        Private Name As String
        Private Templates As New Dictionary(Of Integer, LootTemplate)

        Public Sub New(ByVal Name As String)
            Me.Name = Name
        End Sub

        Public Function GetLoot(ByVal Entry As Integer) As LootTemplate
            If Templates.ContainsKey(Entry) Then
                Return Templates(Entry)
            Else
                Return CreateTemplate(Entry)
            End If
        End Function

        Private Function CreateTemplate(ByVal Entry As Integer) As LootTemplate
            Dim newTemplate As New LootTemplate()
            Templates.Add(Entry, newTemplate)

            Dim MysqlQuery As New DataTable
            WorldDatabase.Query(String.Format("SELECT * FROM {0} WHERE entry = {1};", Name, Entry), MysqlQuery)
            If MysqlQuery.Rows.Count = 0 Then
                Templates(Entry) = Nothing
                Return Nothing ' No results found
            End If

            For Each LootRow As DataRow In MysqlQuery.Rows
                Dim Item As Integer = LootRow.Item("item")
                Dim ChanceOrQuestChance As Single = LootRow.Item("ChanceOrQuestChance")
                Dim GroupID As Byte = LootRow.Item("groupid")
                Dim MinCountOrRef As Integer = LootRow.Item("mincountOrRef")
                Dim MaxCount As Byte = LootRow.Item("maxcount")
                Dim LootCondition As ConditionType = LootRow.Item("lootcondition")
                Dim ConditionValue1 As Integer = LootRow.Item("condition_value1")
                Dim ConditionValue2 As Integer = LootRow.Item("condition_value2")

                Dim newItem As New LootStoreItem(Item, Math.Abs(ChanceOrQuestChance), GroupID, MinCountOrRef, MaxCount, LootCondition, ConditionValue1, ConditionValue2, (ChanceOrQuestChance < 0.0F))
                newTemplate.AddItem(newItem)
            Next

            Return newTemplate
        End Function

    End Class
#End Region
#Region "GroupLootInfo"
    Public Class GroupLootInfo
        Public LootObject As LootObject
        Public LootSlot As Byte

        Public Item As LootItem
        Public Rolls As New List(Of CharacterObject)
        Public Looters As New Dictionary(Of CharacterObject, Integer)(5)

        Public RollTimeoutTimer As Timer = Nothing

        Public Sub Check()
            If Looters.Count = Rolls.Count Then
                'DONE: End loot
                Dim maxRollType As Byte = 0
                For Each looter As KeyValuePair(Of CharacterObject, Integer) In Looters
                    If looter.Value = 1 Then maxRollType = 1
                    If looter.Value = 2 AndAlso maxRollType <> 1 Then maxRollType = 2
                Next
                If maxRollType = 0 Then
                    LootObject.GroupLootInfo.Remove(LootSlot)
                    Dim response As New PacketClass(OPCODES.SMSG_LOOT_ALL_PASSED)
                    response.AddUInt64(LootObject.GUID)
                    response.AddInt32(LootSlot)
                    response.AddInt32(Item.ItemID)
                    response.AddInt32(0)
                    response.AddInt32(0)
                    Broadcast(response)
                    response.Dispose()
                    Exit Sub
                End If



                Dim maxRoll As Integer = -1
                Dim looterCharacter As CharacterObject = Nothing
                For Each looter As KeyValuePair(Of CharacterObject, Integer) In Looters
                    If looter.Value = maxRollType Then
                        Dim rollValue As Byte = Rnd.Next(0, 100)

                        If rollValue > maxRoll Then
                            maxRoll = rollValue
                            looterCharacter = looter.Key
                        End If

                        Dim response As New PacketClass(OPCODES.SMSG_LOOT_ROLL)
                        response.AddUInt64(LootObject.GUID)
                        response.AddInt32(LootSlot)
                        response.AddUInt64(looter.Key.GUID)
                        response.AddInt32(Item.ItemID)
                        response.AddInt32(0)
                        response.AddInt32(0)
                        response.AddInt8(rollValue)
                        response.AddInt8(looter.Value)
                        Broadcast(response)
                        response.Dispose()
                    End If
                Next



                Dim tmpItem As New ItemObject(Item.ItemID, looterCharacter.GUID)
                tmpItem.StackCount = Item.ItemCount

                Dim wonItem As New PacketClass(OPCODES.SMSG_LOOT_ROLL_WON)
                wonItem.AddUInt64(LootObject.GUID)
                wonItem.AddInt32(LootSlot)
                wonItem.AddInt32(Item.ItemID)
                wonItem.AddInt32(0)
                wonItem.AddInt32(0)
                wonItem.AddUInt64(looterCharacter.GUID)
                wonItem.AddInt8(maxRoll)
                wonItem.AddInt8(maxRollType)
                Broadcast(wonItem)

                If looterCharacter.ItemADD(tmpItem) Then
                    looterCharacter.LogLootItem(tmpItem, Item.ItemCount, False, False)

                    LootObject.GroupLootInfo.Remove(LootSlot)
                    LootObject.Items(LootSlot) = Nothing
                Else
                    tmpItem.Delete()
                    LootObject.GroupLootInfo.Remove(LootSlot)
                End If
            End If
        End Sub
        Public Sub Broadcast(ByRef packet As PacketClass)
            For Each c As CharacterObject In Rolls
                c.Client.SendMultiplyPackets(packet)
            Next
        End Sub
        Public Sub EndRoll(ByVal state As Object)
            For Each c As CharacterObject In Rolls
                If Not Looters.ContainsKey(c) Then
                    Looters(c) = 0

                    'DONE: Send roll info
                    Dim response As New PacketClass(OPCODES.SMSG_LOOT_ROLL)
                    response.AddUInt64(LootObject.GUID)
                    response.AddInt32(LootSlot)
                    response.AddUInt64(c.GUID)
                    response.AddInt32(Item.ItemID)
                    response.AddInt32(0)
                    response.AddInt32(0)
                    response.AddInt8(249)
                    response.AddInt8(0)
                    Broadcast(response)
                End If

            Next
            RollTimeoutTimer.Dispose()
            RollTimeoutTimer = Nothing
            Check()
        End Sub
    End Class
#End Region

#Region "Handlers"
    Public Sub On_CMSG_AUTOSTORE_LOOT_ITEM(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 6 Then Exit Sub
        Try
            packet.GetInt16()
            Dim slot As Byte = packet.GetInt8
            Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_AUTOSTORE_LOOT_ITEM [slot={2}]", Client.IP, Client.Port, slot)

            If LootTable.ContainsKey(Client.Character.lootGUID) Then
                CType(LootTable(Client.Character.lootGUID), LootObject).GetLoot(Client, slot)
            Else
                Dim response As New PacketClass(OPCODES.SMSG_INVENTORY_CHANGE_FAILURE)
                response.AddInt8(InventoryChangeFailure.EQUIP_ERR_ALREADY_LOOTED)
                response.AddUInt64(0)
                response.AddUInt64(0)
                response.AddInt8(0)
                Client.Send(response)
                response.Dispose()
            End If
        Catch e As Exception
            Log.WriteLine(LogType.DEBUG, "Error looting item.{0}", vbNewLine & e.ToString)
        End Try
    End Sub
    Public Sub On_CMSG_LOOT_MONEY(ByRef packet As PacketClass, ByRef Client As ClientClass)
        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_LOOT_MONEY", Client.IP, Client.Port)

        If Not LootTable.ContainsKey(Client.Character.lootGUID) Then Exit Sub

        If Client.Character.IsInGroup Then
            'DONE: Party share
            Dim members As List(Of BaseUnit) = GetPartyMembersAroundMe(Client.Character, 100)
            Dim copper As Integer = (CType(LootTable(Client.Character.lootGUID), LootObject).Money \ members.Count) + 1
            CType(LootTable(Client.Character.lootGUID), LootObject).Money = 0

            Dim sharePcket As New PacketClass(OPCODES.SMSG_LOOT_MONEY_NOTIFY)
            sharePcket.AddInt32(copper)
            For Each character As CharacterObject In members
                character.Client.SendMultiplyPackets(sharePcket)

                character.Copper += copper
                character.SetUpdateFlag(EPlayerFields.PLAYER_FIELD_COINAGE, character.Copper)
                character.SaveCharacter()
            Next

            Client.SendMultiplyPackets(sharePcket)
            Client.Character.Copper += copper
            sharePcket.Dispose()
        Else
            'DONE: Not in party
            Dim copper As Integer = CType(LootTable(Client.Character.lootGUID), LootObject).Money
            Client.Character.Copper += copper
            CType(LootTable(Client.Character.lootGUID), LootObject).Money = 0

            Dim lootPacket As New PacketClass(OPCODES.SMSG_LOOT_MONEY_NOTIFY)
            lootPacket.AddInt32(copper)
            Client.Send(lootPacket)
            lootPacket.Dispose()
        End If
        Client.Character.SetUpdateFlag(EPlayerFields.PLAYER_FIELD_COINAGE, Client.Character.Copper)
        Client.Character.SendCharacterUpdate(False)
        Client.Character.SaveCharacter()



        'TODO: Send to party loooters
        Dim response2 As New PacketClass(OPCODES.SMSG_LOOT_CLEAR_MONEY)
        Client.SendMultiplyPackets(response2)
        'Client.Character.SendToNearPlayers(response2)
        response2.Dispose()
    End Sub
    Public Sub On_CMSG_LOOT(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 13 Then Exit Sub
        packet.GetInt16()
        Dim GUID As ULong = packet.GetUInt64
        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_LOOT [GUID={2:X}]", Client.IP, Client.Port, GUID)

        'DONE: Make sure other players sees that you're looting
        Client.Character.cUnitFlags = Client.Character.cUnitFlags Or UnitFlags.UNIT_FLAG_LOOTING
        Client.Character.SetUpdateFlag(EUnitFields.UNIT_FIELD_FLAGS, Client.Character.cUnitFlags)
        Client.Character.SendCharacterUpdate()

        If LootTable.ContainsKey(GUID) Then
            CType(LootTable(GUID), LootObject).SendLoot(Client)
        Else
            SendEmptyLoot(GUID, LootType.LOOTTYPE_CORPSE, Client)
        End If
    End Sub
    Public Sub On_CMSG_LOOT_RELEASE(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 13 Then Exit Sub
        packet.GetInt16()
        Dim GUID As ULong = packet.GetUInt64
        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_LOOT_RELEASE [lootGUID={2:X}]", Client.IP, Client.Port, GUID)

        If Client.Character.spellCasted(CurrentSpellTypes.CURRENT_GENERIC_SPELL) IsNot Nothing Then
            Client.Character.spellCasted(CurrentSpellTypes.CURRENT_GENERIC_SPELL).State = SpellCastState.SPELL_STATE_IDLE
        End If

        'DONE: Remove looting for other players
        Client.Character.cUnitFlags = Client.Character.cUnitFlags And (Not UnitFlags.UNIT_FLAG_LOOTING)
        Client.Character.SetUpdateFlag(EUnitFields.UNIT_FIELD_FLAGS, Client.Character.cUnitFlags)
        Client.Character.SendCharacterUpdate()

        If LootTable.ContainsKey(GUID) Then
            LootTable(GUID).SendRelease(Client)

            'DONE: Remove loot owner
            LootTable(GUID).LootOwner = 0

            Dim LootType As LootType = LootTable(GUID).LootType
            If LootTable(GUID).IsEmpty Then
                'DONE: Delete loot
                LootTable(GUID).Dispose()

                'DONE: Remove loot sing for player
                If GuidIsCreature(GUID) Then
                    If LootType = WS_Loot.LootType.LOOTTYPE_CORPSE Then
                        'DONE: Set skinnable
                        If WORLD_CREATUREs(GUID).CreatureInfo.SkinLootID > 0 Then
                            WORLD_CREATUREs(GUID).cUnitFlags = WORLD_CREATUREs(GUID).cUnitFlags Or UnitFlags.UNIT_FLAG_SKINNABLE
                        End If

                        WORLD_CREATUREs(GUID).cDynamicFlags = 0

                        Dim response As New PacketClass(OPCODES.SMSG_UPDATE_OBJECT)
                        response.AddInt32(1)
                        response.AddInt8(0)
                        Dim UpdateData As New UpdateClass
                        UpdateData.SetUpdateFlag(EUnitFields.UNIT_DYNAMIC_FLAGS, WORLD_CREATUREs(GUID).cDynamicFlags)
                        UpdateData.SetUpdateFlag(EUnitFields.UNIT_FIELD_FLAGS, WORLD_CREATUREs(GUID).cUnitFlags)
                        UpdateData.AddToPacket(response, ObjectUpdateType.UPDATETYPE_VALUES, WORLD_CREATUREs(GUID))
                        WORLD_CREATUREs(GUID).SendToNearPlayers(response)
                        response.Dispose()
                        UpdateData.Dispose()
                    ElseIf LootType = WS_Loot.LootType.LOOTTYPE_SKINNING Then
                        WORLD_CREATUREs(GUID).Despawn()
                    End If

                ElseIf GuidIsGameObject(GUID) AndAlso WORLD_GAMEOBJECTs.ContainsKey(GUID) Then
                    If WORLD_GAMEOBJECTs(GUID).IsConsumeable Then
                        WORLD_GAMEOBJECTs(GUID).State = GameObjectLootState.LOOT_LOOTED
                        WORLD_GAMEOBJECTs(GUID).Despawn()
                    Else
                        WORLD_GAMEOBJECTs(GUID).State = GameObjectLootState.LOOT_UNLOOTED
                    End If

                ElseIf GuidIsItem(GUID) Then

                    Client.Character.ItemREMOVE(GUID, True, True)
                End If

            Else

                'DONE: Send loot for other players
                If GuidIsCreature(GUID) Then
                    If LootType = WS_Loot.LootType.LOOTTYPE_CORPSE Then
                        If WORLD_CREATUREs.ContainsKey(GUID) = False Then
                            LootTable(GUID).Dispose()
                        Else
                            WORLD_CREATUREs(GUID).cDynamicFlags = DynamicFlags.UNIT_DYNFLAG_LOOTABLE

                            Dim response As New PacketClass(OPCODES.SMSG_UPDATE_OBJECT)
                            response.AddInt32(1)
                            response.AddInt8(0)
                            Dim UpdateData As New UpdateClass
                            UpdateData.SetUpdateFlag(EUnitFields.UNIT_DYNAMIC_FLAGS, WORLD_CREATUREs(GUID).cDynamicFlags)
                            UpdateData.AddToPacket(response, ObjectUpdateType.UPDATETYPE_VALUES, CType(WORLD_CREATUREs(GUID), CreatureObject))
                            WORLD_CREATUREs(GUID).SendToNearPlayers(response)
                            response.Dispose()
                            UpdateData.Dispose()
                        End If
                    ElseIf LootType = WS_Loot.LootType.LOOTTYPE_SKINNING Then
                        WORLD_CREATUREs(GUID).Despawn()
                    End If
                ElseIf GuidIsGameObject(GUID) Then
                    If WORLD_GAMEOBJECTs.ContainsKey(GUID) = False OrElse LootTable(GUID).LootType = LootType.LOOTTYPE_FISHING Then
                        LootTable(GUID).Dispose()
                    Else
                        WORLD_GAMEOBJECTs(GUID).State = GameObjectLootState.LOOT_UNLOOTED

                        Dim response As New PacketClass(OPCODES.SMSG_UPDATE_OBJECT)
                        response.AddInt32(1)
                        response.AddInt8(0)
                        Dim UpdateData As New UpdateClass
                        UpdateData.SetUpdateFlag(EGameObjectFields.GAMEOBJECT_STATE, 0, WORLD_GAMEOBJECTs(GUID).State)
                        UpdateData.AddToPacket(response, ObjectUpdateType.UPDATETYPE_VALUES, CType(WORLD_GAMEOBJECTs(GUID), GameObjectObject))

                        WORLD_GAMEOBJECTs(GUID).SendToNearPlayers(response)
                        response.Dispose()
                        UpdateData.Dispose()
                    End If

                ElseIf GuidIsItem(GUID) Then
                    CType(LootTable(GUID), LootObject).Dispose()
                    Client.Character.ItemREMOVE(GUID, True, True)
                Else
                    'DONE: In all other cases - delete the loot
                    CType(LootTable(GUID), LootObject).Dispose()
                End If

            End If
        Else
            Dim responseRelease As New PacketClass(OPCODES.SMSG_LOOT_RELEASE_RESPONSE)
            responseRelease.AddUInt64(GUID)
            responseRelease.AddInt8(1)
            Client.Send(responseRelease)
            responseRelease.Dispose()

            If GuidIsCreature(GUID) Then
                'DONE: Set skinnable
                If WORLD_CREATUREs(GUID).CreatureInfo.SkinLootID > 0 Then
                    WORLD_CREATUREs(GUID).cUnitFlags = WORLD_CREATUREs(GUID).cUnitFlags Or UnitFlags.UNIT_FLAG_SKINNABLE
                End If

                WORLD_CREATUREs(GUID).cDynamicFlags = 0

                Dim response As New PacketClass(OPCODES.SMSG_UPDATE_OBJECT)
                response.AddInt32(1)
                response.AddInt8(0)
                Dim UpdateData As New UpdateClass
                UpdateData.SetUpdateFlag(EUnitFields.UNIT_DYNAMIC_FLAGS, WORLD_CREATUREs(GUID).cDynamicFlags)
                UpdateData.SetUpdateFlag(EUnitFields.UNIT_FIELD_FLAGS, WORLD_CREATUREs(GUID).cUnitFlags)
                UpdateData.AddToPacket(response, ObjectUpdateType.UPDATETYPE_VALUES, WORLD_CREATUREs(GUID))
                WORLD_CREATUREs(GUID).SendToNearPlayers(response)
                response.Dispose()
                UpdateData.Dispose()
            End If
        End If

        Client.Character.lootGUID = 0
    End Sub

    Public Sub SendEmptyLoot(ByVal GUID As ULong, ByVal LootType As LootType, ByRef Client As ClientClass)
        Dim response As New PacketClass(OPCODES.SMSG_LOOT_RESPONSE)
        response.AddUInt64(GUID)
        response.AddInt8(LootType)
        response.AddInt32(0)
        response.AddInt8(0)
        Client.Send(response)
        response.Dispose()
#If DEBUG Then
        Log.WriteLine(LogType.WARNING, "[{0}:{1}] Empty loot for GUID [{2:X}].", Client.IP, Client.Port, GUID)
#End If
    End Sub

    Public Sub StartRoll(ByVal LootGUID As ULong, ByVal Slot As Byte, ByRef Character As CharacterObject)
        Dim rollCharacters As New List(Of CharacterObject)

        rollCharacters.Add(Character)
        For Each GUID As ULong In Character.Group.LocalMembers
            If Character.playersNear.Contains(GUID) Then rollCharacters.Add(CHARACTERs(GUID))
        Next

        Dim startRoll As New PacketClass(OPCODES.SMSG_LOOT_START_ROLL)
        startRoll.AddUInt64(LootGUID)
        startRoll.AddInt32(Slot)

        startRoll.AddInt32(LootTable(LootGUID).GroupLootInfo(Slot).Item.ItemID)
        startRoll.AddInt32(0)
        startRoll.AddInt32(0)
        startRoll.AddInt32(60000)

        For Each c As CharacterObject In rollCharacters
            c.Client.SendMultiplyPackets(startRoll)
        Next
        startRoll.Dispose()

        CType(CType(LootTable(LootGUID), LootObject).GroupLootInfo(CType(Slot, Byte)), GroupLootInfo).Rolls = rollCharacters
        CType(CType(LootTable(LootGUID), LootObject).GroupLootInfo(CType(Slot, Byte)), GroupLootInfo).RollTimeoutTimer = New Timer(AddressOf CType(CType(LootTable(LootGUID), LootObject).GroupLootInfo(CType(Slot, Byte)), GroupLootInfo).EndRoll, 0, 60000, Timeout.Infinite)
    End Sub
    Public Sub On_CMSG_LOOT_ROLL(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 18 Then Exit Sub
        packet.GetInt16()
        Dim GUID As ULong = packet.GetUInt64
        Dim Slot As Byte = packet.GetInt32
        Dim rollType As Byte = packet.GetInt8

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_LOOT_ROLL [loot={2} roll={3}]", Client.IP, Client.Port, GUID, rollType)


        '0 - Pass
        '1 - Need
        '2 - Greed

        'DONE: Send roll info
        Dim response As New PacketClass(OPCODES.SMSG_LOOT_ROLL)
        response.AddUInt64(GUID)
        response.AddInt32(Slot)
        response.AddUInt64(Client.Character.GUID)
        response.AddInt32(CType(CType(CType(LootTable(GUID), LootObject).GroupLootInfo(Slot), GroupLootInfo).Item, LootItem).ItemID)
        response.AddInt32(0)
        response.AddInt32(0)

        'FIRST:  0: "Need for: [item name]" > 127: "you passed on: [item name]"
        'SECOND: 0: "Need for: [item name]" 0: "You have selected need for [item name] 1: need roll 2: greed roll
        Select Case rollType
            Case 0
                response.AddInt8(249)
                response.AddInt8(0)
            Case 1
                response.AddInt8(0)
                response.AddInt8(0)
            Case 2
                response.AddInt8(249)
                response.AddInt8(2)
        End Select



        CType(CType(LootTable(GUID), LootObject).GroupLootInfo(CType(Slot, Byte)), GroupLootInfo).Broadcast(response)
        response.Dispose()

        CType(CType(LootTable(GUID), LootObject).GroupLootInfo(CType(Slot, Byte)), GroupLootInfo).Looters(Client.Character) = rollType
        CType(CType(LootTable(GUID), LootObject).GroupLootInfo(CType(Slot, Byte)), GroupLootInfo).Check()
    End Sub
#End Region

End Module
