Imports System.Timers
Imports System.Runtime.InteropServices
Imports System.IO
Imports System.Text
Imports System.Xml
Imports System.Xml.Linq

'Public Enum ServiceState
'    SERVICE_STOPPED = 1
'    SERVICE_START_PENDING = 2
'    SERVICE_STOP_PENDING = 3
'    SERVICE_RUNNING = 4
'    SERVICE_CONTINUE_PENDING = 5
'    SERVICE_PAUSE_PENDING = 6
'    SERVICE_PAUSED = 7
'End Enum

'<StructLayout(LayoutKind.Sequential)>
'Public Structure ServiceStatus
'    Public dwServiceType As Long
'    Public dwCurrentState As ServiceState
'    Public dwControlsAccepted As Long
'    Public dwWin32ExitCode As Long
'    Public dwServiceSpecificExitCode As Long
'    Public dwCheckPoint As Long
'    Public dwWaitHint As Long
'End Structure

Public Class AkadoTVChannels
    Private aTimer As System.Timers.Timer

    'Declare Auto Function SetServiceStatus Lib "advapi32.dll" (ByVal handle As IntPtr, ByRef serviceStatus As ServiceStatus) As Boolean

    Private Sub TestStartupAndStop(ByVal args() As String)

        Me.OnStart(args)
        Console.ReadLine()
        Me.OnStop()
    End Sub

    Public Sub New()

        'MyBase.New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        'Me.EventLog1 = New System.Diagnostics.EventLog
        'If Not System.Diagnostics.EventLog.SourceExists("AkadoTVChannelsSource") Then
        '    System.Diagnostics.EventLog.CreateEventSource("AkadoTVChannelsSource", "AkadoTVChannelsLog")
        'End If
        'EventLog1.Source = "AkadoTVChannelsSource"
        'EventLog1.Log = "AkadoTVChannelsLog"

    End Sub

    Protected Overrides Sub OnStart(ByVal args() As String)
        '' Update the service state to Start Pending.
        'Dim serviceStatus As ServiceStatus = New ServiceStatus()
        'serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING
        'serviceStatus.dwWaitHint = 100000
        'SetServiceStatus(Me.ServiceHandle, serviceStatus)

        ' Add code here to start your service. This method should set things
        ' in motion so your service can do its work.
        EventLog1.WriteEntry(" ===> Service " + ServiceName + " OnStart")
        If GetChannelsList() And GetEPG() Then
            EventLog1.WriteEntry("Akado IPTV channelList and EPG were updated", EventLogEntryType.Information)
        Else
            EventLog1.WriteEntry("Failed to update Akado IPTV channelList and EPG", EventLogEntryType.Warning)
        End If
        SetPollTimer()

        '' Update the service state to Running.
        'serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING
        'SetServiceStatus(Me.ServiceHandle, serviceStatus)

    End Sub

    Protected Overrides Sub OnStop()
        '' Update the service state to Stop Pending.
        'Dim serviceStatus As ServiceStatus = New ServiceStatus()
        'serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING
        'serviceStatus.dwWaitHint = 100000
        'SetServiceStatus(Me.ServiceHandle, serviceStatus)

        ' Add code here to perform any tear-down necessary to stop your service.
        EventLog1.WriteEntry(">=== Service " + ServiceName + " OnStop")

        '' Update the service state to Running.
        'serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED
        'SetServiceStatus(Me.ServiceHandle, serviceStatus)

    End Sub

    Private Sub SetPollTimer()
        ' Create a timer with a 6 second interval.
        aTimer = New System.Timers.Timer(My.Settings.pollTimerMinutes * 60000)
        ' Hook up the Elapsed event for the timer. 
        AddHandler aTimer.Elapsed, AddressOf OnTimedEvent
        aTimer.AutoReset = True
        aTimer.Enabled = True
    End Sub

    ' The event handler for the Timer.Elapsed event. 
    Private Sub OnTimedEvent(source As Object, e As System.Timers.ElapsedEventArgs)
        If Not File.Exists(My.Settings.tvListSavePath) Or
               File.GetLastWriteTime(My.Settings.tvListSavePath).AddMinutes(My.Settings.agingTimeMinutes) < Now() Then
            If GetChannelsList() And GetEPG() Then
                EventLog1.WriteEntry("Akado IPTV channelList and EPG were updated", EventLogEntryType.Information)
            Else
                EventLog1.WriteEntry("Failed to update Akado IPTV channelList or EPG", EventLogEntryType.Warning)
            End If
        End If
    End Sub

    Private Function GetChannelsList() As Boolean
        Dim wtURL = My.Settings.channelsListURL
        Dim channelsPath = My.Settings.tvListSavePath

        If File.Exists(channelsPath) Then
            'File.Move(channelsPath, channelsPath + ".old")
            Try
                File.Delete(channelsPath)
            Catch ex As Exception
                Return False
            End Try
        End If

        Dim fs As FileStream = File.Create(channelsPath)
        Try
            AddText(fs, "#EXTM3U tvg-shift=" + My.Settings.tvEpgShift & Environment.NewLine)
            Dim wtList = XDocument.Load(wtURL)
            Dim streams = From s In wtList...<stream> Select s

            For Each s In streams
                'AddText(fs, "#EXTINF:-1" &
                '        " tvg-id=" & """" & s.@id & """" &
                '        " tvg-name=" & """" & Replace(Replace(s.@title, "[TV] ", ""), " ", "_") & """" &
                '        " tvg-logo=" & """" & Replace(Replace(s.@title, "[TV] ", ""), " ", "_") & ".png""" &
                '        ", " & Replace(s.@title, "[TV] ", "") & Environment.NewLine)
                AddText(fs, "#EXTINF:-1" &
                        " tvg-id=" & """" & s.@id & """" &
                        " tvg-name=" & """" & Replace(Replace(s.@title, "[TV] ", ""), " ", "_") & """" &
                        " tvg-logo=" & """" & s.@id & ".png""" &
                        ", " & Replace(s.@title, "[TV] ", "") & Environment.NewLine)
                AddText(fs, s.@uri & Environment.NewLine)
            Next
        Catch ex As Exception
            fs.Close()
            'File.Move(channelsPath + ".old", channelsPath)
            Return False
        End Try
        fs.Close()
        'If File.Exists(channelsPath + ".old") Then
        '    File.Delete(channelsPath + ".old")
        'End If
        Return True
    End Function

    Private Function GetEPG() As Boolean
        Dim wtURL = My.Settings.epgURL
        Dim epgPath = My.Settings.tvEpgSavePath
        Try
            Dim wtEPG = XElement.Load(wtURL)

            Dim xmlEPG As XElement =
                     <tv>
                         <%= From pr In wtEPG...<program>
                             Select
                         <channel id=<%= pr.@id %>>
                             <display-name lang="ru"><%= Replace(Replace(pr.<title>.Value, "]]", ""), "![CDATA[", "") %></display-name>
                             <icon></icon>
                         </channel>
                         %>
                         <%= From it In wtEPG...<item>
                             Select
                             <programme start=<%= epgTime(it.<start>.Value) %> stop=<%= epgTime(it.<stop>.Value) %> channel=<%= it.Parent.Parent.@id %>>
                                 <title lang="ru"><%= Replace(Replace(it.<title>.Value, "]]", ""), "![CDATA[", "") %></title>
                             </programme>
                         %>
                     </tv>
            If File.Exists(epgPath) Then
                Try
                    File.Delete(epgPath)
                Catch ex As Exception
                    Return False
                End Try
            End If
            xmlEPG.Save(epgPath)
        Catch ex As Exception
            Return False
        End Try
        Return True
    End Function

    Private Function epgTime(ByVal sTime As String) As String
        Dim d = DateAdd(DateInterval.Second, CDbl(sTime), #1/1/1970 00:00#)
        epgTime = Format(Year(d), "00") &
                  Format(Month(d), "00") &
                  Format(Day(d), "00") &
                  Format(Hour(d), "00") &
                  Format(Minute(d), "00") &
                  Format(Second(d), "00") & " +0000"
    End Function

    Private Sub AddText(ByVal fs As FileStream, ByVal value As String)
        Dim enc As Encoding = Encoding.UTF8
        Dim info As Byte() = enc.GetBytes(value)
        fs.Write(info, 0, info.Length)
    End Sub

End Class
