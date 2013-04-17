﻿Imports System.Threading


Public Class ServerController

    Private cmdConnection As CasparCGConnection
    Private tickConnection As CasparCGConnection
    Private updateConnection As CasparCGConnection
    Private testConnection As CasparCGConnection
    Private updateThread As Thread
    Private tickThread As Thread
    Private serverAddress As String = "localhost"
    Private serverPort As Integer = 5250
    Private testChannel As Integer = 4
    Private channels As Integer
    Private opened As Boolean
    Private WithEvents ticker As FrameTicker
    Private updater As mediaUpdater
    Private playlist As IPlaylistItem ' Die Root Playlist unter die alle anderen kommen

    Public Sub New()
        playlist = New PlaylistBlockItem("Playlist", Me, 1, 1)
    End Sub

    Public Sub open()
        open(serverAddress, serverPort)
    End Sub

    Public Sub close()
        logger.debug("Close servercontroller...")
        opened = False
        If Not IsNothing(updateThread) Then updateThread.Abort()
        If Not IsNothing(tickThread) Then tickThread.Abort()
        updateConnection.close()
        tickConnection.close()
        testConnection.close()
        cmdConnection.close()
    End Sub

    Public Function isOpen() As Boolean
        Return opened
    End Function

    Public Sub open(ByVal serverAddress As String, ByVal severPort As Integer)
        opened = True
        Me.serverAddress = serverAddress
        Me.serverPort = serverPort
        cmdConnection = New CasparCGConnection(serverAddress, serverPort)
        cmdConnection.connect()
        updateConnection = New CasparCGConnection(serverAddress, serverPort)
        updateConnection.connect()
        testConnection = New CasparCGConnection(serverAddress, serverPort)
        testConnection.connect()
        tickConnection = New CasparCGConnection(serverAddress, serverPort)
        tickConnection.connect()

        ' Channels des Servers bestimmen
        channels = readServerChannels()

        ' Tick Thread starten
        ticker = New FrameTicker(tickConnection, Me, , 5)
        tickThread = New Thread(AddressOf ticker.tick)
        'tickThread.Start()

        ' updater starten
        updater = New mediaUpdater(updateConnection, playlist, Me)
    End Sub

    Public Sub update()
        updater.updateMedia(Nothing, Nothing)
    End Sub

    Public Function getPlaylistRoot() As IPlaylistItem
        Return playlist
    End Function

    Public Function getCommandConnection() As CasparCGConnection
        Return cmdConnection
    End Function

    Public Function getChannels() As Integer
        Return channels
    End Function

    Private Function readServerChannels() As Integer
        Dim ch As Integer = 0
        Dim response = testConnection.sendCommand(CasparCGCommandFactory.getInfo())
        If response.isOK Then
            Dim lineArray() = response.getData.Split(vbLf)
            If Not IsNothing(lineArray) Then
                ch = lineArray.Length
            End If
        End If
        Return ch
    End Function


    ''' <summary>
    ''' Returns the media duration in milliseconds if playing in native fps.
    ''' </summary>
    ''' <param name="media"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function getOriginalMediaDuration(ByRef media As CasparCGMedia) As Long
        Select Case media.getMediaType
            Case CasparCGMedia.MediaType.COLOR, CasparCGMedia.MediaType.STILL, CasparCGMedia.MediaType.TEMPLATE
                '' These mediatyps doesn't have any durations
                Return 0
            Case Else
                If media.getInfos.Count = 0 Then
                    '' no media info is loaded
                    '' load it now
                    media.parseXML(getMediaInfo(media))
                End If
                If media.containsInfo("nb-frames") AndAlso media.containsInfo("fps") AndAlso media.containsInfo("progressive") Then
                    Dim fps As Integer = Integer.Parse(media.getInfo("fps"))
                    If Not Boolean.Parse(media.getInfo("progressive")) Then
                        fps = fps / 2
                    End If
                    Return getTimeInMS(media.getInfo("nb-frames"), fps)
                End If
                logger.err("Could not get media duration of " & media.getFullName & "(" & media.getMediaType.ToString & ").")
                Return 0
        End Select
    End Function

    ''' <summary>
    ''' Returns the media duration in milliseconds if playing at a given channel.
    ''' </summary>
    ''' <param name="media"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function getMediaDuration(ByRef media As CasparCGMedia, ByVal channel As Integer) As Long
        Select Case media.getMediaType
            Case CasparCGMedia.MediaType.COLOR, CasparCGMedia.MediaType.STILL, CasparCGMedia.MediaType.TEMPLATE
                '' These mediatyps doesn't have any durations
                Return 0
            Case Else
                If media.getInfos.Count = 0 Then
                    '' no media info is loaded
                    '' load it now
                    media.parseXML(getMediaInfo(media))
                End If
                If media.containsInfo("nb-frames") Then
                    Return getTimeInMS(media.getInfo("nb-frames"), getFPS(channel))
                End If
                logger.err("Could not get media duration of " & media.getFullName & "(" & media.getMediaType.ToString & ").")
                Return 0
        End Select
    End Function

    Public Function getPlayingMediaNames(ByVal channel As Integer, ByVal layer As Integer) As IEnumerable(Of String)
        Dim names As New List(Of String)
        Return names
    End Function

    ''' <summary>
    ''' Returns whether or not, the given channel is configured at the connected CasparCGServer
    ''' </summary>
    ''' <param name="channel">the channel number to check for</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function containsChannel(ByVal channel As Integer) As Boolean
        If Not IsNothing(testConnection) Then
            Return testConnection.sendCommand(CasparCGCommandFactory.getInfo(channel)).isOK
        Else
            Return False
        End If
    End Function

    ''' <summary>
    ''' Returns the smallest free layer of the given channel
    ''' </summary>
    ''' <param name="channel"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function getFreeLayer(ByVal channel As Integer) As Integer
        Dim layer As Integer = 0
        While Not isLayerFree(layer, channel)
            layer = layer + 1
        End While
        Return layer
    End Function

    ''' <summary>
    ''' Returns whether or not a layer of a channel is free, which means no producer is playing on it.
    ''' </summary>
    ''' <param name="layer"></param>
    ''' <param name="channel"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function isLayerFree(ByVal layer As Integer, ByVal channel As Integer, Optional ByVal onlyForeground As Boolean = False, Optional ByVal onlyBackground As Boolean = False) As Boolean
        Dim answer = testConnection.sendCommand(CasparCGCommandFactory.getInfo(channel, layer, onlyBackground, onlyForeground))
        Dim doc As New MSXML2.DOMDocument()
        If answer.isOK AndAlso doc.loadXML(answer.getXMLData) Then
            For Each type As MSXML2.IXMLDOMNode In doc.getElementsByTagName("type")
                If Not type.nodeTypedValue.Equals("empty-producer") Then
                    Return False
                End If
            Next
            Return True
        End If
        If Not IsNothing(doc.parseError) Then
            logger.warn("Error checking layer." & vbNewLine & doc.parseError.reason & vbNewLine & doc.parseError.line & ":" & doc.parseError.linepos & vbNewLine & doc.parseError.srcText)
        Else
            logger.warn("Could not check layer. Server response was incorrect.")
        End If
        Return False
    End Function

    ''' <summary>
    ''' Returns the time in milliseconds needed to play the given number of frames at a specified framerate.
    ''' </summary>
    ''' <param name="frames">the number of frames</param>
    ''' <param name="fps">the framerate multiplied by 100 to avoid floating numbers like 59.94.</param> 
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function getTimeInMS(ByVal frames As Long, ByVal fps As Integer) As Long
        Return (frames * 1000) / (fps / 100)
    End Function

    ''' <summary>
    ''' Returns the framerate of the spezified channel or 0 if the channel does not exist.
    ''' </summary>
    ''' <param name="channel"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function getFPS(ByVal channel As Integer) As Integer
        Dim result = testConnection.sendCommand(CasparCGCommandFactory.getInfo(channel))
        Dim infoDoc As New MSXML2.DOMDocument
        If infoDoc.loadXML(result.getXMLData()) Then
            If infoDoc.hasChildNodes Then
                If Not IsNothing(infoDoc.selectSingleNode("channel")) AndAlso Not IsNothing(infoDoc.selectSingleNode("channel").selectSingleNode("video-mode")) Then
                    Select Case infoDoc.selectSingleNode("channel").selectSingleNode("video-mode").nodeTypedValue
                        Case "PAL"
                            Return 2500
                        Case "NTSC"
                            Return 2994
                        Case Else
                            If infoDoc.selectSingleNode("channel").selectSingleNode("video-mode").nodeTypedValue.Contains("i") Then
                                Return Integer.Parse(infoDoc.selectSingleNode("channel").selectSingleNode("video-mode").nodeTypedValue.Substring(infoDoc.selectSingleNode("channel").selectSingleNode("video-mode").nodeTypedValue.IndexOf("i") + 1)) / 2
                            ElseIf infoDoc.selectSingleNode("channel").selectSingleNode("video-mode").nodeTypedValue.Contains("p") Then
                                Return Integer.Parse(infoDoc.selectSingleNode("channel").selectSingleNode("video-mode").nodeTypedValue.Substring(infoDoc.selectSingleNode("channel").selectSingleNode("video-mode").nodeTypedValue.IndexOf("p") + 1))
                            End If
                    End Select
                End If
            End If
        Else
            logger.err("Could not get channel fps. Error in server response: " & infoDoc.parseError.reason & " @" & vbNewLine & result.getServerMessage)
        End If
        Return 0
    End Function

    Public Function getMediaInfo(ByRef media As CasparCGMedia) As String
        If media.getMediaType = CasparCGMedia.MediaType.TEMPLATE Then
            Dim response = testConnection.sendCommand(CasparCGCommandFactory.getInfo(media))
            If response.isOK Then
                Return response.getXMLData
            Else
                logger.err("Error loading xml data received from server for " & media.toString)
                logger.err("ServerMessage dump: " & response.getServerMessage)
            End If
        Else
            Dim layer = getFreeLayer(testChannel)
            Dim response = testConnection.sendCommand(CasparCGCommandFactory.getLoadbg(testChannel, layer, media.getFullName))
            If response.isOK Then
                Dim infoDoc As New MSXML2.DOMDocument
                response = testConnection.sendCommand(CasparCGCommandFactory.getInfo(testChannel, layer, True))
                testConnection.sendAsyncCommand(CasparCGCommandFactory.getCGClear(testChannel, layer))
                If infoDoc.loadXML(response.getXMLData()) AndAlso Not IsNothing(infoDoc.selectSingleNode("producer").selectSingleNode("destination")) Then
                    If infoDoc.selectSingleNode("producer").selectSingleNode("destination").selectSingleNode("producer").selectSingleNode("type").nodeTypedValue.Equals("separated-producer") Then
                        Return infoDoc.selectSingleNode("producer").selectSingleNode("destination").selectSingleNode("producer").selectSingleNode("fill").selectSingleNode("producer").xml
                    Else
                        Return infoDoc.selectSingleNode("producer").selectSingleNode("destination").selectSingleNode("producer").xml
                    End If
                Else
                    logger.err("Error loading xml data received from server for " & media.toString & ". Error: " & infoDoc.parseError.reason)
                    logger.err("ServerMessages dump: " & response.getServerMessage)
                End If
            Else
                logger.err("Error getting media information. Server messages was: " & response.getServerMessage)
            End If
        End If
        Return ""
    End Function

    ''' <summary>
    ''' Returns a Dictionary of all media and templates on the server key by their names.
    ''' If withMediaInfo is true, all mediaItems will have filled mediaInfo which is default by need more time.
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function getMediaList(Optional ByVal withMediaInfo As Boolean = True) As Dictionary(Of String, CasparCGMedia)
        Dim media As New Dictionary(Of String, CasparCGMedia)
        '' Catch the media list and create the media objects
        Dim response = testConnection.sendCommand(CasparCGCommandFactory.getCls)
        If response.isOK Then
            For Each line As String In response.getData.Split(vbCrLf)
                line = line.Trim()
                If line <> "" AndAlso line.Split(" ").Length > 2 Then
                    Dim name = line.Substring(1, line.LastIndexOf("""") - 1).ToUpper
                    line = line.Remove(0, line.LastIndexOf("""") + 1)
                    line = line.Trim().Replace("""", "").Replace("  ", " ")
                    Dim values() = line.Split(" ")
                    Select Case values(0)
                        Case "MOVIE"
                            media.Add(name, New CasparCGMovie(name))
                        Case "AUDIO"
                            media.Add(name, New CasparCGAudio(name))
                        Case "STILL"
                            media.Add(name, New CasparCGStill(name))
                    End Select
                End If
            Next
        End If

        '' Catch the template list and create the template objects
        response = testConnection.sendCommand(CasparCGCommandFactory.getTls)
        If response.isOK Then
            For Each line As String In response.getData.Split(vbCrLf)
                line = line.Trim.Replace(vbCr, "").Replace(vbLf, "")
                If line <> "" AndAlso line.Split(" ").Length > 2 Then
                    Dim name = line.Substring(1, line.LastIndexOf("""") - 1).ToUpper
                    media.Add(name, New CasparCGTemplate(name))
                End If
            Next
        End If

        '' Add mediaInfo if requested
        If withMediaInfo Then
            For Each item In media.Values
                item.parseXML(getMediaInfo(item))
            Next
        End If

        Return media
    End Function

    Public Function getTicker() As FrameTicker
        Return ticker
    End Function

    Public Sub stopTicker()
        If Not IsNothing(tickThread) AndAlso tickThread.IsAlive Then
            tickThread.Abort()
        End If
    End Sub

    Public Sub startTicker()
        If Not IsNothing(tickThread) AndAlso Not tickThread.IsAlive AndAlso Not IsNothing(ticker) Then
            tickThread = New Thread(AddressOf ticker.tick)
            tickThread.Start()
        End If
    End Sub

    Public Sub toggleTickerActive()
        If tickThread.IsAlive Then
            stopTicker()
        Else
            startTicker()
        End If
    End Sub

End Class

''' <summary>
''' A ticker class which raises events if a frame change is noticed in one of the casparCG channels. 
''' There is no waranty that a tick will be rissen for every framechange at all, but the frame number send should be close to the real one.
''' The period of ticks is close to "per frame" but will increase with every handler added for the frameTickEvent. 
''' Nevertheless, the precision of the frame number given by the event will not be affect much by increasing the eventHandler count.
''' During each poll request to the server for the current frame number, a short period of interpolated ticks will be rissen.
''' These are only calculated and not proofed to be in correct sync to the server. The length of this period could be configured.
''' Use bigger values if you have a poor network  conncetion. Default is 5 seconds (5000ms). It is possible that there will be no 
''' interpolated tick at all if the proccessing of the poll and raising the event takes longer than the given period.
''' In that case, you will only get received values form the server when they arrive.
''' It is very likley that the given frame number is behind the frame number at the servers channel by a few frames.
''' Since this delay may differ depending on your networkconnection and hardware, it should be, more or less, constant 
''' and will be minimized by a simple compensation technique.
''' 
''' To take load off your cpu, you could set a upper bound to the frequency at which frameTickEvents should be rissen.
''' As a default, it will be tried to raise one for every frame. But this is just a trie. Depending on your hardware and the number
''' of EventHandler, it may be only every few frames.
''' If you don't need high frequencies, use higher values to take load off your cpu.
''' Take in mind, that the bound will be related to the channel with the highest fps. So if you have a channel with p25 and one with p50 and
''' a frameInterval of 4, channel p50 will tick not more than every 4 frames but channel p25 could still tick every 2 frames.
'''  
''' Start tick() in a new Thread and register handlers for the frameTick event. 
''' Keep in mind to use delegates since the event will likely to be rissen by a different thread.
''' </summary>
''' <remarks></remarks>
Public Class FrameTicker
    Private sc As ServerController
    Private con As CasparCGConnection
    Public interpolationTime As Integer
    Private frameInterval As Integer
    Private channels As Integer
    Private channelFameTime() As Integer
    Private channelLastUpdate() As Long
    Private channelFrame As New Dictionary(Of Integer, Long)
    Private minFrameTime As Integer = Integer.MaxValue

    Public Event frameTick(ByVal sender As Object, ByVal e As frameTickEventArgs)

    ''' <summary>
    ''' Creates a new ticker instance.
    ''' </summary>
    ''' <param name="con">the connection to poll the channels for the actual frame number</param>
    ''' <param name="controller">the servercontroler</param>
    ''' <param name="interpolationTime">the number of milliseconds between each servercall. In that time, the frame tick will be interpolated by a local timer which may differ from the servers real values.</param>
    ''' <param name="frameInterval">the desired interval in which frameTickEvents should be rissen. This is just a desired value and will only give a upper bound but not a lower bound. Default is 1 tick per frame</param> 
    ''' <remarks></remarks>
    Public Sub New(ByRef con As CasparCGConnection, ByVal controller As ServerController, Optional ByVal interpolationTime As Integer = 5000, Optional ByVal frameInterval As Integer = 1)
        sc = controller
        Me.con = con
        Me.interpolationTime = interpolationTime
        Me.frameInterval = frameInterval

        channels = sc.getChannels 'Anzahl der Channels bestimmen

        ReDim channelFameTime(channels)
        ReDim channelLastUpdate(channels)
        For i As Integer = 1 To channels
            channelFameTime(i - 1) = 1000 / (sc.getFPS(i) / 100)
            If minFrameTime > channelFameTime(i - 1) - 1 Then minFrameTime = channelFameTime(i - 1)
            channelFrame.Add(i, 0)
        Next

        logger.debug("Ticker init by thread " & Thread.CurrentThread.ManagedThreadId)
    End Sub

    Public Sub tick()
        logger.debug("Ticker thread " & Thread.CurrentThread.ManagedThreadId & " started")
        Dim timer As New Stopwatch ' Timer to measure the time it takes to calc current frame / channel and inform listeners
        Dim offsetTimer As New Stopwatch
        Dim iterationStart As Long
        Dim iterationEnd As Long
        Dim interpolatingSince As Integer

        Dim infoDoc As New MSXML2.DOMDocument
        Dim frame As Long = 0
        Dim ch = 0
        timer.Start()
        offsetTimer.Start()
        While True
            ' Alle channels durchgehen
            For channel As Integer = 1 To channels
                ch = channel - 1
                'werte einlesen
                offsetTimer.Restart()
                infoDoc.loadXML(con.sendCommand(CasparCGCommandFactory.getInfo(channel, Integer.MaxValue)).getXMLData)
                frame = infoDoc.firstChild.selectSingleNode("frame-number").nodeTypedValue + (offsetTimer.ElapsedMilliseconds / 2 / channelFameTime(ch))
                offsetTimer.Stop()

                ' Korrigierten Wert für die Frame number berechen aus dem rückgabewert des servers + der frames die in der bearbeitungszeit
                ' vermeintlich vergangen sind. Wir gehen vereinfacht davon aus, das die hälfte der Zeit für den Rückweg 
                ' vom Server zu uns gebaucht wurde da wir das nicht genau messen können.
                If frame <> channelFrame.Item(channel) Then
                    channelFrame.Item(channel) = frame
                End If
            Next
            ' Event auslösen
            RaiseEvent frameTick(Me, New frameTickEventArgs(channelFrame))
            ' Jetzt ein paar frames nur rechnen und dann wieder mit dem Serverwert vergleichen
            Dim hasChanged = False
            interpolatingSince = timer.ElapsedMilliseconds
            While timer.ElapsedMilliseconds - interpolatingSince < interpolationTime
                iterationStart = timer.ElapsedMilliseconds
                For channel As Integer = 0 To channels - 1
                    ' Interpoliere frames indem wir die vergange zeit der der letzen aktualisierung betrachten.
                    ' Ist sie größer als die frameTime müssen wir die frame ändern.
                    If iterationStart - channelLastUpdate(channel) >= channelFameTime(channel) Then
                        hasChanged = True
                        channelFrame.Item(channel + 1) = channelFrame.Item(channel + 1) + ((iterationStart - channelLastUpdate(channel)) / channelFameTime(channel))
                        channelLastUpdate(channel) = iterationStart
                    End If
                Next
                '' Event auslösen
                If hasChanged Then RaiseEvent frameTick(Me, New frameTickEventArgs(channelFrame))
                hasChanged = False

                ' Jetzt noch ein bisschen warten um die cpu zu entlasten. Mindestens bis die nächste frame möglich ist
                ' oder soviele Frames wie im frameInterval angegeben
                iterationEnd = timer.ElapsedMilliseconds
                If iterationEnd - iterationStart < (minFrameTime * frameInterval) Then
                    Thread.Sleep((minFrameTime * frameInterval) - (iterationEnd - iterationStart))
                End If
            End While
        End While
    End Sub
End Class

''' <summary>
''' The frameTick event result containing the channel which ticked and the actual framenumber.
''' </summary>
''' <remarks></remarks>
Public Class frameTickEventArgs
    Inherits EventArgs

    Public Sub New(ByVal result As Dictionary(Of Integer, Long))
        _result = result
    End Sub

    Private _result As Dictionary(Of Integer, Long)
    Public ReadOnly Property result
        Get
            Return _result
        End Get
    End Property
End Class

Public Class mediaUpdater

    Private ready As New Semaphore(1, 1)
    Private controller As ServerController
    Private WithEvents ticker As FrameTicker
    Private updateConnection As CasparCGConnection
    Private channels As Integer
    Private playlist As IPlaylistItem

    ' Global um häufiges Alloc in updateMedia zu verhindern
    Private activeItems() As Dictionary(Of Integer, Dictionary(Of String, IPlaylistItem))
    Private infoDoc As New MSXML2.DOMDocument
    Private layer As Integer
    Private mediaName As String
    Dim xml As String
    Private foregroundProducer As MSXML2.IXMLDOMElement

    Public Sub New(ByRef updateConnection As CasparCGConnection, ByRef playlist As IPlaylistItem, ByRef controller As ServerController)
        Me.controller = controller
        Me.updateConnection = updateConnection
        Me.channels = controller.getChannels
        Me.playlist = playlist

        ReDim activeItems(channels)
        For i = 0 To channels - 1
            activeItems(i) = New Dictionary(Of Integer, Dictionary(Of String, IPlaylistItem))
        Next

        ticker = controller.getTicker

        AddHandler ticker.frameTick, AddressOf updateMedia
    End Sub

    ''' <summary>
    ''' Updates all playing media items in the playlist
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Public Sub updateMedia(ByVal sender As Object, ByVal e As frameTickEventArgs) 'Handles ticker.frameTick
        ''
        '' reads in alle channels as xml
        '' and checks the state of each layer
        '' if a media is found, the corresponding 
        '' IPlaylistItem will be searched within the active 
        '' Items and updated. If no one of the active Items 
        '' is not playing anymore, it will be set to stopped.
        ''

        ' Damit nicht zu viele updates gleichzeitig laufen, 
        ' muss jedes update exlusiv updaten. Kann es das in einer milliseconde
        ' nicht erreichen, verwirft es das update für diesen Tick
        If ready.WaitOne(1) Then

            '' Listen und variablen vorbereiten
            xml = ""
            mediaName = ""
            For Each item In playlist.getPlayingChildItems(True, True)
                logger.err("Found playing: " & item.getName)
                If activeItems(item.getChannel - 1).ContainsKey(item.getLayer) Then
                    activeItems(item.getChannel - 1).Item(item.getLayer).Add(item.getMedia().getName, item)
                Else
                    activeItems(item.getChannel - 1).Add(item.getLayer, New Dictionary(Of String, IPlaylistItem))
                    activeItems(item.getChannel - 1).Item(item.getLayer).Add(item.getMedia.getName, item)
                End If
            Next

            For c = 0 To channels - 1
                Dim response = updateConnection.sendCommand(CasparCGCommandFactory.getInfo(c + 1))
                If infoDoc.loadXML(response.getXMLData) Then

                    '' Über alle layer iter.
                    For Each layerNode As MSXML2.IXMLDOMElement In infoDoc.getElementsByTagName("layer")
                        layer = Integer.Parse(layerNode.selectSingleNode("index").nodeTypedValue())

                        ' Ich brauche das layer nur zu beachten, wenn es auch aktive Items auf diesem layer gibt
                        If activeItems(c).ContainsKey(layer) Then

                            '' Den producer im Vordergrund holen. Falls eine Transition im gang ist, müssen wir schauen
                            '' wo das video von interesse liegt, bzw. beide verarbeiten
                            foregroundProducer = layerNode.selectSingleNode("foreground").selectSingleNode("producer")
                            If foregroundProducer.selectSingleNode("type").nodeTypedValue.Equals("transition-producer") Then

                                '' Source und Dest einzeln betrachten
                                'foregroundProducer = foregroundProducer.selectSingleNode("producer")

                                '' !!! Vorsicht, einer der beiden medien die spielen gehen hier verloren!!! TODO
                                If foregroundProducer.selectSingleNode("source").selectSingleNode("producer").selectSingleNode("type").nodeTypedValue.Equals("ffmpeg-producer") Then
                                    ' Name und xml holen
                                    mediaName = foregroundProducer.selectSingleNode("source").selectSingleNode("producer").selectSingleNode("filename").nodeTypedValue
                                    xml = foregroundProducer.selectSingleNode("source").selectSingleNode("producer").xml
                                ElseIf foregroundProducer.selectSingleNode("destination").selectSingleNode("producer").selectSingleNode("type").nodeTypedValue.Equals("ffmpeg-producer") Then
                                    ' Name und xml holen
                                    mediaName = foregroundProducer.selectSingleNode("destination").selectSingleNode("producer").selectSingleNode("filename").nodeTypedValue
                                    xml = foregroundProducer.selectSingleNode("destination").selectSingleNode("producer").xml
                                End If
                            ElseIf foregroundProducer.selectSingleNode("type").nodeTypedValue.Equals("ffmpeg-producer") Then
                                ' Name und Xml holen
                                mediaName = foregroundProducer.selectSingleNode("filename").nodeTypedValue
                                xml = foregroundProducer.xml
                            End If

                            ' Pfad und extension wegschneiden
                            If mediaName.Length > 0 Then
                                mediaName = mediaName.Substring(mediaName.IndexOf("\") + 1, mediaName.LastIndexOf(".") - (mediaName.IndexOf("\") + 1))
                                If activeItems(c).Item(layer).ContainsKey(mediaName) Then
                                    '' Daten updaten
                                    activeItems(c).Item(layer).Item(mediaName).getMedia.parseXML(xml)
                                    ''danach aus liste entfernen
                                    activeItems(c).Item(layer).Remove(mediaName)
                                End If
                            End If
                            If activeItems(c).Item(layer).Count = 0 Then Exit For
                        End If
                    Next
                    ' Alle Items in diesem Channel die jetzt noch in der liste sind, sind nicht mehr auf dem Server gestartet 
                    ' und werden daher als gestoppt markiert
                    For Each layer As Integer In activeItems(c).Keys
                        For Each item As IPlaylistItem In activeItems(c).Item(layer).Values
                            item.stoppedPlaying()
                        Next
                    Next
                    activeItems(c).Clear()
                Else
                    logger.err("Could not update media at channel " & c + 1 & ". Unable to load xml data. " & infoDoc.parseError.reason)
                End If
            Next
        End If
        ready.Release()
    End Sub
End Class



