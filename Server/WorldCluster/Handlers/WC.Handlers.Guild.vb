﻿' 
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
Imports System.Net.Sockets
Imports System.Xml.Serialization
Imports System.IO
Imports System.Net
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports SpuriousZero.Common.BaseWriter
Imports SpuriousZero.Common


Public Module WC_Handlers_Guild

    Public Sub On_CMSG_GUILD_QUERY(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 9 Then Exit Sub
        packet.GetInt16()
        Dim GuildID As UInteger = packet.GetUInt32

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_QUERY [{2}]", Client.IP, Client.Port, GuildID)

        SendGuildQuery(Client, GuildID)
    End Sub
    Public Sub On_CMSG_GUILD_ROSTER(ByRef packet As PacketClass, ByRef Client As ClientClass)
        'packet.GetInt16()

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_ROSTER", Client.IP, Client.Port)

        SendGuildRoster(Client.Character)
    End Sub
    Public Sub On_CMSG_GUILD_CREATE(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 6 Then Exit Sub
        packet.GetInt16()
        Dim guildName As String = packet.GetString

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_CREATE [{2}]", Client.IP, Client.Port, guildName)

        If Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_ALREADY_IN_GUILD)
            Exit Sub
        End If

        'DONE: Create guild data
        Dim MySQLQuery As New DataTable
        CharacterDatabase.Query(String.Format("INSERT INTO guilds (guild_name, guild_leader, guild_cYear, guild_cMonth, guild_cDay) VALUES (""{0}"", {1}, {2}, {3}, {4}); SELECT guild_id FROM guilds WHERE guild_name = ""{0}"";", guildName, Client.Character.GUID, Now.Year - 2006, Now.Month, Now.Day), MySQLQuery)

        AddCharacterToGuild(Client.Character, MySQLQuery.Rows(0).Item("guild_id"), 0)
    End Sub
    Public Sub On_CMSG_GUILD_INFO(ByRef packet As PacketClass, ByRef Client As ClientClass)
        packet.GetInt16()

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_INFO", Client.IP, Client.Port)

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        End If

        Dim response As New PacketClass(OPCODES.SMSG_GUILD_INFO)
        response.AddString(Client.Character.Guild.Name)
        response.AddInt32(Client.Character.Guild.cDay)
        response.AddInt32(Client.Character.Guild.cMonth)
        response.AddInt32(Client.Character.Guild.cYear)
        response.AddInt32(0)
        response.AddInt32(0)
        Client.Send(response)
        response.Dispose()
    End Sub



    'Guild Leader Options
    Public Enum GuildRankRights
        GR_RIGHT_EMPTY = &H40
        GR_RIGHT_GCHATLISTEN = &H41
        GR_RIGHT_GCHATSPEAK = &H42
        GR_RIGHT_OFFCHATLISTEN = &H44
        GR_RIGHT_OFFCHATSPEAK = &H48
        GR_RIGHT_PROMOTE = &HC0
        GR_RIGHT_DEMOTE = &H140
        GR_RIGHT_INVITE = &H50
        GR_RIGHT_REMOVE = &H60
        GR_RIGHT_SETMOTD = &H1040
        GR_RIGHT_EPNOTE = &H2040
        GR_RIGHT_VIEWOFFNOTE = &H4040
        GR_RIGHT_EOFFNOTE = &H8040
        GR_RIGHT_ALL = &HF1FF
    End Enum
    Public Sub On_CMSG_GUILD_RANK(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 14 Then Exit Sub
        packet.GetInt16()
        Dim rankID As Integer = packet.GetInt32
        Dim rankRights As UInteger = packet.GetUInt32
        Dim rankName As String = packet.GetString.Replace("""", "_").Replace("'", "_")

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_RANK [{2}:{3}:{4}]", Client.IP, Client.Port, rankID, rankRights, rankName)
        If rankID < 0 OrElse rankID > 9 Then Exit Sub

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildLeader Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        End If

        Client.Character.Guild.Ranks(rankID) = rankName
        Client.Character.Guild.RankRights(rankID) = rankRights

        CharacterDatabase.Update(String.Format("UPDATE guilds SET guild_rank{1} = ""{2}"", guild_rank{1}_Rights = {3} WHERE guild_id = {0};", Client.Character.Guild.ID, rankID, rankName, rankRights))

        SendGuildQuery(Client, Client.Character.Guild.ID)
        SendGuildRoster(Client.Character)
    End Sub
    Public Sub On_CMSG_GUILD_ADD_RANK(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 6 Then Exit Sub
        packet.GetInt16()
        Dim NewRankName As String = packet.GetString().Replace("""", "_").Replace("'", "_")

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_ADD_RANK [{2}]", Client.IP, Client.Port, NewRankName)

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildLeader Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        ElseIf ValidateGuildName(NewRankName) = False Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_INTERNAL)
            Exit Sub
        End If

        For i As Integer = 0 To 9
            If Client.Character.Guild.Ranks(i) = "" Then
                Client.Character.Guild.Ranks(i) = NewRankName
                Client.Character.Guild.RankRights(i) = GuildRankRights.GR_RIGHT_GCHATLISTEN Or GuildRankRights.GR_RIGHT_GCHATSPEAK
                CharacterDatabase.Update(String.Format("UPDATE guilds SET guild_rank{1} = '{2}', guild_rank{1}_Rights = '{3}' WHERE guild_id = {0};", Client.Character.Guild.ID, i, NewRankName, Client.Character.Guild.RankRights(i)))

                SendGuildQuery(Client, Client.Character.Guild.ID)
                SendGuildRoster(Client.Character)
                Exit Sub
            End If
        Next

        SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_INTERNAL)
    End Sub
    Public Sub On_CMSG_GUILD_DEL_RANK(ByRef packet As PacketClass, ByRef Client As ClientClass)
        packet.GetInt16()

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_DEL_RANK", Client.IP, Client.Port)

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildLeader Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        End If

        'TODO: Check if someone in the guild is the rank we're removing?
        'TODO: Can we really remove all ranks?
        For i As Integer = 9 To 0 Step -1
            If Client.Character.Guild.Ranks(i) <> "" Then
                CharacterDatabase.Update(String.Format("UPDATE guilds SET guild_rank{1} = '{2}', guild_rank{1}_Rights = '{3}' WHERE guild_id = {0};", Client.Character.Guild.ID, i, "", 0))

                SendGuildQuery(Client, Client.Character.Guild.ID)
                SendGuildRoster(Client.Character)
                Exit Sub
            End If
        Next

        SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_INTERNAL)
    End Sub
    Public Sub On_CMSG_GUILD_LEADER(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 6 Then Exit Sub
        packet.GetInt16()
        Dim playerName As String = packet.GetString

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_LEADER [{2}]", Client.IP, Client.Port, playerName)
        If playerName.Length < 2 Then Exit Sub
        playerName = CapitalizeName(playerName)

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildLeader Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        End If

        'DONE: Find new leader's GUID
        Dim MySQLQuery As New DataTable
        CharacterDatabase.Query("SELECT char_guid, char_guildId, char_guildrank FROM characters WHERE char_name = '" & playerName & "';", MySQLQuery)
        If MySQLQuery.Rows.Count = 0 Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_PLAYER_NOT_FOUND, playerName)
            Exit Sub
        ElseIf CUInt(MySQLQuery.Rows(0).Item("char_guildId")) <> Client.Character.Guild.ID Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD_S, playerName)
            Exit Sub
        End If
        Dim PlayerGUID As ULong = MySQLQuery.Rows(0).Item("char_guid")

        Client.Character.GuildRank = 1 'Officer
        Client.Character.SendGuildUpdate()
        If CHARACTERs.ContainsKey(PlayerGUID) Then
            CHARACTERs(PlayerGUID).GuildRank = 0
            CHARACTERs(PlayerGUID).SendGuildUpdate()
        End If
        Client.Character.Guild.Leader = PlayerGUID
        CharacterDatabase.Update(String.Format("UPDATE guilds SET guild_leader = ""{1}"" WHERE guild_id = {0};", Client.Character.Guild.ID, PlayerGUID))
        CharacterDatabase.Update(String.Format("UPDATE characters SET char_guildRank = {0} WHERE char_guid = {1};", 0, PlayerGUID))
        CharacterDatabase.Update(String.Format("UPDATE characters SET char_guildRank = {0} WHERE char_guid = {1};", Client.Character.GuildRank, Client.Character.GUID))

        'DONE: Send notify message
        Dim response As New PacketClass(OPCODES.SMSG_GUILD_EVENT)
        response.AddInt8(GuildEvent.LEADER_CHANGED)
        response.AddInt8(2)
        response.AddString(Client.Character.Name)
        response.AddString(playerName)
        BroadcastToGuild(response, Client.Character.Guild)
        response.Dispose()
    End Sub
    Public Sub On_MSG_SAVE_GUILD_EMBLEM(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If packet.Data.Length < 34 Then Exit Sub
        packet.GetInt16()
        Dim unk0 As Integer = packet.GetInt32
        Dim unk1 As Integer = packet.GetInt32
        Dim tEmblemStyle As Integer = packet.GetInt32
        Dim tEmblemColor As Integer = packet.GetInt32
        Dim tBorderStyle As Integer = packet.GetInt32
        Dim tBorderColor As Integer = packet.GetInt32
        Dim tBackgroundColor As Integer = packet.GetInt32

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] MSG_SAVE_GUILD_EMBLEM [{2},{3}] [{4}:{5}:{6}:{7}:{8}]", Client.IP, Client.Port, unk0, unk1, tEmblemStyle, tEmblemColor, tBorderStyle, tBorderColor, tBackgroundColor)

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildLeader Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub

            'TODO: Check if you have enough money
            'ElseIf Client.Character.Copper < 100000 Then
            '    SendInventoryChangeFailure(Client.Character, InventoryChangeFailure.EQUIP_ERR_NOT_ENOUGH_MONEY, 0, 0)
            '    Exit Sub
        End If

        Client.Character.Guild.EmblemStyle = tEmblemStyle
        Client.Character.Guild.EmblemColor = tEmblemColor
        Client.Character.Guild.BorderStyle = tBorderStyle
        Client.Character.Guild.BorderColor = tBorderColor
        Client.Character.Guild.BackgroundColor = tBackgroundColor

        CharacterDatabase.Update(String.Format("UPDATE guilds SET guild_tEmblemStyle = {1}, guild_tEmblemColor = {2}, guild_tBorderStyle = {3}, guild_tBorderColor = {4}, guild_tBackgroundColor = {5} WHERE guild_id = {0};", Client.Character.Guild.ID, tEmblemStyle, tEmblemColor, tBorderStyle, tBorderColor, tBackgroundColor))

        SendGuildQuery(Client, Client.Character.Guild.ID)

        Dim packet_event As New PacketClass(OPCODES.SMSG_GUILD_EVENT)
        packet_event.AddInt8(GuildEvent.TABARDCHANGE)
        packet_event.AddInt32(Client.Character.Guild.ID)
        BroadcastToGuild(packet_event, Client.Character.Guild)
        packet_event.Dispose()

        'TODO: This tabard design costs 10g!
        'Client.Character.Copper -= 100000
        'Client.Character.SetUpdateFlag(EPlayerFields.PLAYER_FIELD_COINAGE, Client.Character.Copper)
        'Client.Character.SendCharacterUpdate(False)
    End Sub
    Public Sub On_CMSG_GUILD_DISBAND(ByRef packet As PacketClass, ByRef Client As ClientClass)
        'packet.GetInt16()

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_DISBAND", Client.IP, Client.Port)

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildLeader Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        End If


        'DONE: Clear all members
        Dim response As New PacketClass(OPCODES.SMSG_GUILD_EVENT)
        response.AddInt8(GuildEvent.DISBANDED)
        response.AddInt8(0)

        Dim GuildID As Integer = Client.Character.Guild.ID

        Dim tmpArray() As ULong = Client.Character.Guild.Members.ToArray
        For Each Member As ULong In tmpArray
            If CHARACTERs.ContainsKey(Member) Then
                RemoveCharacterFromGuild(CHARACTERs(Member))
                CHARACTERs(Member).Client.SendMultiplyPackets(response)
            Else
                RemoveCharacterFromGuild(Member)
            End If
        Next

        GUILDs(GuildID).Dispose()

        response.Dispose()

        'DONE: Delete guild information
        CharacterDatabase.Update("DELETE FROM guilds WHERE guild_id = " & GuildID & ";")
    End Sub

    Public Sub On_CMSG_GUILD_MOTD(ByRef packet As PacketClass, ByRef Client As ClientClass)
        'Isn't the client even sending a null terminator for the motd if it's empty?
        If (packet.Data.Length - 1) < 6 Then Exit Sub
        packet.GetInt16()
        Dim Motd As String = ""
        If packet.Length <> 4 Then Motd = packet.GetString.Replace("""", "_").Replace("'", "_")

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_MOTD", Client.IP, Client.Port)

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildRightSet(GuildRankRights.GR_RIGHT_SETMOTD) Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        End If

        Client.Character.Guild.Motd = Motd
        CharacterDatabase.Update(String.Format("UPDATE guilds SET guild_MOTD = '{1}' WHERE guild_id = '{0}';", Client.Character.Guild.ID, Motd))

        Dim response As New PacketClass(OPCODES.SMSG_GUILD_EVENT)
        response.AddInt8(GuildEvent.MOTD)
        response.AddInt8(1)
        response.AddString(Motd)

        'DONE: Send message to everyone in the guild
        BroadcastToGuild(response, Client.Character.Guild)

        response.Dispose()
    End Sub
    Public Sub On_CMSG_GUILD_SET_OFFICER_NOTE(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 6 Then Exit Sub
        packet.GetInt16()
        Dim playerName As String = packet.GetString
        If (packet.Data.Length - 1) < (6 + playerName.Length + 1) Then Exit Sub
        Dim Note As String = packet.GetString

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_SET_OFFICER_NOTE [{2}]", Client.IP, Client.Port, playerName)

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildRightSet(GuildRankRights.GR_RIGHT_EOFFNOTE) Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        End If

        CharacterDatabase.Update(String.Format("UPDATE characters SET char_guildOffNote = ""{1}"" WHERE char_name = ""{0}"";", playerName, Note.Replace("""", "_").Replace("'", "_")))

        SendGuildRoster(Client.Character)
    End Sub
    Public Sub On_CMSG_GUILD_SET_PUBLIC_NOTE(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 6 Then Exit Sub
        packet.GetInt16()
        Dim playerName As String = packet.GetString
        If (packet.Data.Length - 1) < (6 + playerName.Length + 1) Then Exit Sub
        Dim Note As String = packet.GetString

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_SET_PUBLIC_NOTE [{2}]", Client.IP, Client.Port, playerName)

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildRightSet(GuildRankRights.GR_RIGHT_EPNOTE) Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        End If

        CharacterDatabase.Update(String.Format("UPDATE characters SET char_guildPNote = ""{1}"" WHERE char_name = ""{0}"";", playerName, Note.Replace("""", "_").Replace("'", "_")))

        SendGuildRoster(Client.Character)
    End Sub
    Public Sub On_CMSG_GUILD_REMOVE(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 6 Then Exit Sub
        packet.GetInt16()
        Dim playerName As String = packet.GetString.Replace("'", "_").Replace("""", "_")

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_REMOVE [{2}]", Client.IP, Client.Port, playerName)
        If playerName.Length < 2 Then Exit Sub
        playerName = CapitalizeName(playerName)

        'DONE: Player1 checks
        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildRightSet(GuildRankRights.GR_RIGHT_REMOVE) Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        End If

        'DONE: Find player2's guid
        Dim q As New DataTable
        CharacterDatabase.Query("SELECT char_guid FROM characters WHERE char_name = '" & playerName & "';", q)

        'DONE: Removed checks
        If q.Rows.Count = 0 Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_PLAYER_NOT_FOUND, playerName)
            Exit Sub
        ElseIf Not CHARACTERs.ContainsKey(CType(q.Rows(0).Item("char_guid"), ULong)) Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_PLAYER_NOT_FOUND, playerName)
            Exit Sub
        End If

        Dim c As CharacterObject = CHARACTERs(CType(q.Rows(0).Item("char_guid"), ULong))

        If c.IsGuildLeader Then
            SendGuildResult(Client, GuildCommand.GUILD_QUIT_S, GuildError.GUILD_LEADER_LEAVE)
            Exit Sub
        End If

        'DONE: Send guild event
        Dim response As New PacketClass(OPCODES.SMSG_GUILD_EVENT)
        response.AddInt8(GuildEvent.REMOVED)
        response.AddInt8(2)
        response.AddString(playerName)
        response.AddString(c.Name)
        BroadcastToGuild(response, Client.Character.Guild)
        response.Dispose()

        RemoveCharacterFromGuild(c)
    End Sub
    Public Sub On_CMSG_GUILD_PROMOTE(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 6 Then Exit Sub
        packet.GetInt16()
        Dim playerName As String = CapitalizeName(packet.GetString)

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_PROMOTE [{2}]", Client.IP, Client.Port, playerName)

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildRightSet(GuildRankRights.GR_RIGHT_PROMOTE) Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        End If

        'DONE: Find promoted player's guid
        Dim q As New DataTable
        CharacterDatabase.Query("SELECT char_guid FROM characters WHERE char_name = '" & playerName.Replace("'", "_") & "';", q)

        'DONE: Promoted checks
        If q.Rows.Count = 0 Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_NAME_INVALID)
            Exit Sub
        ElseIf Not CHARACTERs.ContainsKey(CType(q.Rows(0).Item("char_guid"), ULong)) Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_PLAYER_NOT_FOUND, playerName)
            Exit Sub
        End If
        Dim c As CharacterObject = CHARACTERs(CType(q.Rows(0).Item("char_guid"), ULong))
        If c.Guild.ID <> Client.Character.Guild.ID Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD_S, playerName)
            Exit Sub
        ElseIf c.GuildRank <= Client.Character.GuildRank Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        ElseIf c.GuildRank = GUILD_RANK_MIN Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_INTERNAL)
            Exit Sub
        End If

        'DONE: Do the real update            
        c.GuildRank -= 1
        CharacterDatabase.Update(String.Format("UPDATE characters SET char_guildRank = {0} WHERE char_guid = {1};", c.GuildRank, c.GUID))
        c.SendGuildUpdate()

        'DONE: Send event to guild
        Dim response As New PacketClass(OPCODES.SMSG_GUILD_EVENT)
        response.AddInt8(GuildEvent.PROMOTION)
        response.AddInt8(3)
        response.AddString(c.Name)
        response.AddString(playerName)
        response.AddString(Client.Character.Guild.Ranks(c.GuildRank))
        BroadcastToGuild(response, Client.Character.Guild)
        response.Dispose()
    End Sub
    Public Sub On_CMSG_GUILD_DEMOTE(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 6 Then Exit Sub
        packet.GetInt16()
        Dim playerName As String = CapitalizeName(packet.GetString)

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_DEMOTE [{2}]", Client.IP, Client.Port, playerName)

        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildRightSet(GuildRankRights.GR_RIGHT_PROMOTE) Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        End If

        'DONE: Find demoted player's guid
        Dim q As New DataTable
        CharacterDatabase.Query("SELECT char_guid FROM characters WHERE char_name = '" & playerName.Replace("'", "_") & "';", q)

        'DONE: Demoted checks
        If q.Rows.Count = 0 Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_NAME_INVALID)
            Exit Sub
        ElseIf Not CHARACTERs.ContainsKey(CType(q.Rows(0).Item("char_guid"), ULong)) Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_PLAYER_NOT_FOUND, playerName)
            Exit Sub
        End If
        Dim c As CharacterObject = CHARACTERs(CType(q.Rows(0).Item("char_guid"), ULong))
        If c.Guild.ID <> Client.Character.Guild.ID Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD_S, playerName)
            Exit Sub
        ElseIf c.GuildRank <= Client.Character.GuildRank Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        ElseIf c.GuildRank = GUILD_RANK_MAX Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_INTERNAL)
            Exit Sub
        End If

        'DONE: Max defined rank check
        If Trim(Client.Character.Guild.Ranks(c.GuildRank + 1)) = "" Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_INTERNAL)
            Exit Sub
        End If

        'DONE: Do the real update            
        c.GuildRank += 1
        CharacterDatabase.Update(String.Format("UPDATE characters SET char_guildRank = {0} WHERE char_guid = {1};", c.GuildRank, c.GUID))
        c.SendGuildUpdate()

        'DONE: Send event to guild
        Dim response As New PacketClass(OPCODES.SMSG_GUILD_EVENT)
        response.AddInt8(GuildEvent.DEMOTION)
        response.AddInt8(3)
        response.AddString(c.Name)
        response.AddString(playerName)
        response.AddString(Client.Character.Guild.Ranks(c.GuildRank))
        BroadcastToGuild(response, Client.Character.Guild)
        response.Dispose()
    End Sub

    'User Options
    Public Sub On_CMSG_GUILD_INVITE(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 6 Then Exit Sub
        packet.GetInt16()
        Dim playerName As String = packet.GetString

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_INVITE [{2}]", Client.IP, Client.Port, playerName)

        'DONE: Inviter checks
        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Not Client.Character.IsGuildRightSet(GuildRankRights.GR_RIGHT_INVITE) Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PERMISSIONS)
            Exit Sub
        End If

        'DONE: Find invited player's guid
        Dim q As New DataTable
        CharacterDatabase.Query("SELECT char_guid FROM characters WHERE char_name = '" & playerName.Replace("'", "_") & "';", q)

        'DONE: Invited checks
        If q.Rows.Count = 0 Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_NAME_INVALID)
            Exit Sub
        ElseIf Not CHARACTERs.ContainsKey(CType(q.Rows(0).Item("char_guid"), ULong)) Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_PLAYER_NOT_FOUND, playerName)
            Exit Sub
        End If

        Dim c As CharacterObject = CHARACTERs(CType(q.Rows(0).Item("char_guid"), ULong))
        If c.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.ALREADY_IN_GUILD, playerName)
            Exit Sub
        ElseIf c.Side <> Client.Character.Side Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.GUILD_NOT_ALLIED, playerName)
            Exit Sub
        ElseIf c.GuildInvited <> 0 Then
            SendGuildResult(Client, GuildCommand.GUILD_INVITE_S, GuildError.ALREADY_INVITED_TO_GUILD, playerName)
            Exit Sub
        End If

        Dim response As New PacketClass(OPCODES.SMSG_GUILD_INVITE)
        response.AddString(Client.Character.Name)
        response.AddString(Client.Character.Guild.Name)
        c.Client.Send(response)
        response.Dispose()

        c.GuildInvited = Client.Character.Guild.ID
        c.GuildInvitedBy = Client.Character.GUID
    End Sub

    Public Sub On_CMSG_GUILD_ACCEPT(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If Client.Character.GuildInvited = 0 Then Throw New ApplicationException("Character accepting guild invitation whihtout being invited.")

        AddCharacterToGuild(Client.Character, Client.Character.GuildInvited)
        Client.Character.GuildInvited = 0

        Dim response As New PacketClass(OPCODES.SMSG_GUILD_EVENT)
        response.AddInt8(GuildEvent.JOINED)
        response.AddInt8(1)
        response.AddString(Client.Character.Name)
        BroadcastToGuild(response, Client.Character.Guild)
        response.Dispose()

        SendGuildRoster(Client.Character)
        SendGuildMOTD(Client.Character)
    End Sub
    Public Sub On_CMSG_GUILD_DECLINE(ByRef packet As PacketClass, ByRef Client As ClientClass)
        Client.Character.GuildInvited = 0

        If CHARACTERs.ContainsKey(CType(Client.Character.GuildInvitedBy, Long)) Then
            Dim response As New PacketClass(OPCODES.SMSG_GUILD_DECLINE)
            response.AddString(Client.Character.Name)
            CHARACTERs(CType(Client.Character.GuildInvitedBy, Long)).Client.Send(response)
            response.Dispose()
        End If
    End Sub
    Public Sub On_CMSG_GUILD_LEAVE(ByRef packet As PacketClass, ByRef Client As ClientClass)
        'packet.GetInt16()

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_GUILD_LEAVE", Client.IP, Client.Port)

        'DONE: Checks
        If Not Client.Character.IsInGuild Then
            SendGuildResult(Client, GuildCommand.GUILD_CREATE_S, GuildError.GUILD_PLAYER_NOT_IN_GUILD)
            Exit Sub
        ElseIf Client.Character.IsGuildLeader Then
            SendGuildResult(Client, GuildCommand.GUILD_QUIT_S, GuildError.GUILD_LEADER_LEAVE)
            Exit Sub
        End If

        RemoveCharacterFromGuild(Client.Character)
        SendGuildResult(Client, GuildCommand.GUILD_QUIT_S, GuildError.GUILD_PLAYER_NO_MORE_IN_GUILD, Client.Character.Name)

        Dim response As New PacketClass(OPCODES.SMSG_GUILD_EVENT)
        response.AddInt8(GuildEvent.LEFT)
        response.AddInt8(1)
        response.AddString(Client.Character.Name)
        BroadcastToGuild(response, Client.Character.Guild)
        response.Dispose()
    End Sub

    Public Enum PetitionTurnInError As Integer
        PETITIONTURNIN_OK = 0                   ':Closes the window
        PETITIONTURNIN_ALREADY_IN_GUILD = 2     'You are already in a guild
        PETITIONTURNIN_NEED_MORE_SIGNATURES = 4 'You need more signatures
    End Enum

    Public Sub On_CMSG_TURN_IN_PETITION(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 13 Then Exit Sub
        packet.GetInt16()
        Dim ItemGUID As ULong = packet.GetUInt64

        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_TURN_IN_PETITION [GUID={2:X}]", Client.IP, Client.Port, ItemGUID)

        'DONE: Get info
        Dim q As New DataTable
        CharacterDatabase.Query("SELECT * FROM petitions WHERE petition_itemGuid = " & ItemGUID - GUID_ITEM & " LIMIT 1;", q)
        If q.Rows.Count = 0 Then Exit Sub
        Dim Type As Byte = q.Rows(0).Item("petition_type")
        Dim Name As String = q.Rows(0).Item("petition_name")

        'DONE: Check if already in guild
        If Type = 9 AndAlso Client.Character.IsInGuild Then
            Dim response As New PacketClass(OPCODES.SMSG_TURN_IN_PETITION_RESULTS)
            response.AddInt32(PetitionTurnInError.PETITIONTURNIN_ALREADY_IN_GUILD)
            Client.Send(response)
            response.Dispose()
            Exit Sub
        End If

        'DONE: Check required signs
        Dim RequiredSigns As Byte = 9
        If CType(q.Rows(0).Item("petition_signedMembers"), Integer) < RequiredSigns Then
            Dim response As New PacketClass(OPCODES.SMSG_TURN_IN_PETITION_RESULTS)
            response.AddInt32(PetitionTurnInError.PETITIONTURNIN_NEED_MORE_SIGNATURES)
            Client.Send(response)
            response.Dispose()
            Exit Sub
        End If

        Dim q2 As New DataTable

        'DONE: Create guild and add members
        CharacterDatabase.Query(String.Format("INSERT INTO guilds (guild_name, guild_leader, guild_cYear, guild_cMonth, guild_cDay) VALUES ('{0}', {1}, {2}, {3}, {4}); SELECT guild_id FROM guilds WHERE guild_name = '{0}';", Name, Client.Character.GUID, Now.Year - 2006, Now.Month, Now.Day), q2)

        AddCharacterToGuild(Client.Character, q2.Rows(0).Item("guild_id"), 0)

        'DONE: Adding 9 more signed characters
        For i As Byte = 1 To 9
            If CHARACTERs.ContainsKey(CType(q.Rows(0).Item("petition_signedMember" & i), ULong)) Then
                AddCharacterToGuild(CHARACTERs(CType(q.Rows(0).Item("petition_signedMember" & i), ULong)), q2.Rows(0).Item("guild_id"))
            Else
                AddCharacterToGuild(CType(q.Rows(0).Item("petition_signedMember" & i), ULong), q2.Rows(0).Item("guild_id"))
            End If
        Next

        'DONE: Delete guild charter item, on the world server
        Client.Character.GetWorld.ClientPacket(Client.Index, packet.Data)

        Dim success As New PacketClass(OPCODES.SMSG_TURN_IN_PETITION_RESULTS)
        success.AddInt32(0) 'Okay
        Client.Send(success)
        success.Dispose()
    End Sub

End Module