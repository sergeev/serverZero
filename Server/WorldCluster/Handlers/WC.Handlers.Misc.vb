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


Public Module WC_Handlers_Misc


    Public Sub On_CMSG_QUERY_TIME(ByRef packet As PacketClass, ByRef Client As ClientClass)
        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_QUERY_TIME", Client.IP, Client.Port)
        Dim response As New PacketClass(OPCODES.SMSG_QUERY_TIME_RESPONSE)
        response.AddInt32(timeGetTime) 'GetTimestamp(Now))
        Client.Send(response)
        response.Dispose()
        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] SMSG_QUERY_TIME_RESPONSE", Client.IP, Client.Port)
    End Sub


    Public Sub On_CMSG_NEXT_CINEMATIC_CAMERA(ByRef packet As PacketClass, ByRef Client As ClientClass)
        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_NEXT_CINEMATIC_CAMERA", Client.IP, Client.Port)
    End Sub
    Public Sub On_CMSG_COMPLETE_CINEMATIC(ByRef packet As PacketClass, ByRef Client As ClientClass)
        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_COMPLETE_CINEMATIC", Client.IP, Client.Port)
    End Sub


    Public Sub On_CMSG_PLAYED_TIME(ByRef packet As PacketClass, ByRef Client As ClientClass)
        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_NAME_QUERY", Client.IP, Client.Port)

        Dim response As New PacketClass(OPCODES.SMSG_PLAYED_TIME)
        response.AddInt32(1)
        response.AddInt32(1)
        Client.Send(response)
        response.Dispose()
    End Sub
    Public Sub On_CMSG_NAME_QUERY(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If (packet.Data.Length - 1) < 13 Then Exit Sub
        packet.GetInt16()
        Dim GUID As ULong = packet.GetUInt64()
        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_NAME_QUERY [GUID={2:X}]", Client.IP, Client.Port, GUID)

        If GuidIsPlayer(GUID) AndAlso CHARACTERs.ContainsKey(GUID) Then
            Dim SMSG_NAME_QUERY_RESPONSE As New PacketClass(OPCODES.SMSG_NAME_QUERY_RESPONSE)
            SMSG_NAME_QUERY_RESPONSE.AddUInt64(GUID)
            SMSG_NAME_QUERY_RESPONSE.AddString(CHARACTERs(GUID).Name)
            SMSG_NAME_QUERY_RESPONSE.AddInt32(CHARACTERs(GUID).Race)
            SMSG_NAME_QUERY_RESPONSE.AddInt32(CHARACTERs(GUID).Gender)
            SMSG_NAME_QUERY_RESPONSE.AddInt32(CHARACTERs(GUID).Classe)
            SMSG_NAME_QUERY_RESPONSE.AddInt8(0)
            Client.Send(SMSG_NAME_QUERY_RESPONSE)
            SMSG_NAME_QUERY_RESPONSE.Dispose()
            Exit Sub
        Else
            'DONE: Send it to the world server if it wasn't found in the cluster
            Try
                Client.Character.GetWorld.ClientPacket(Client.Index, packet.Data)
            Catch
                WS.Disconnect("NULL", New Integer() {Client.Character.Map})
            End Try
        End If
    End Sub
    Public Sub On_CMSG_INSPECT(ByRef packet As PacketClass, ByRef Client As ClientClass)
        packet.GetInt16()
        Dim GUID As ULong = packet.GetUInt64
        Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_INSPECT [GUID={2:X}]", Client.IP, Client.Port, GUID)
    End Sub


    Public Sub On_MSG_MOVE_HEARTBEAT(ByRef packet As PacketClass, ByRef Client As ClientClass)
        Try
            Client.Character.GetWorld.ClientPacket(Client.Index, packet.Data)
        Catch
            WS.Disconnect("NULL", New Integer() {Client.Character.Map})
            Exit Sub
        End Try

        'DONE: Save location on cluster
        Client.Character.PositionX = packet.GetFloat(15)
        Client.Character.PositionY = packet.GetFloat
        Client.Character.PositionZ = packet.GetFloat

        'DONE: Sync your location to other party / raid members
        If Client.Character.IsInGroup Then
            Dim statsPacket As New PacketClass(OPCODES.MSG_NULL_ACTION)
            statsPacket.Data = Client.Character.GetWorld.GroupMemberStats(Client.Character.GUID, PartyMemberStatsFlag.GROUP_UPDATE_FLAG_POSITION + PartyMemberStatsFlag.GROUP_UPDATE_FLAG_ZONE)
            Client.Character.Group.BroadcastToOutOfRange(statsPacket, Client.Character)
            statsPacket.Dispose()
        End If
    End Sub
    Public Sub On_CMSG_CANCEL_TRADE(ByRef packet As PacketClass, ByRef Client As ClientClass)
        If Client.Character IsNot Nothing AndAlso Client.Character.IsInWorld Then
            Try
                Client.Character.GetWorld.ClientPacket(Client.Index, packet.Data)
            Catch
                WS.Disconnect("NULL", New Integer() {Client.Character.Map})
            End Try
        Else
            Log.WriteLine(LogType.DEBUG, "[{0}:{1}] CMSG_CANCEL_TRADE", Client.IP, Client.Port)
        End If
    End Sub

End Module
