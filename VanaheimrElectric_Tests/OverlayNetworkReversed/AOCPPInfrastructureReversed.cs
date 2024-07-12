﻿﻿/*
 * Copyright (c) 2015-2024 GraphDefined GmbH
 * This file is part of WWCP OCPI <https://github.com/OpenChargingCloud/WWCP_OCPI>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

using cloud.charging.open.protocols.OCPP;
using cloud.charging.open.protocols.OCPPv2_1.CS;
using cloud.charging.open.protocols.OCPPv2_1.CSMS2;
using cloud.charging.open.protocols.OCPPv2_1.Gateway;
using cloud.charging.open.protocols.OCPPv2_1.NetworkingNode;
using cloud.charging.open.protocols.OCPPv2_1.LocalController;

#endregion

namespace cloud.charging.open.vanaheimr.electric.UnitTests
{

    /// <summary>
    /// Charging infrastructure test defaults using an OCPP Overlay Network
    /// consisting of a CSMS, an OCPP Gateway, an OCPP Local Controller and
    /// three Charging Stations.
    /// 
    /// The HTTP Web Socket connections are initiated in "reversed order" from
    /// the CSMS to the Gateway, to the Local Controller and finally to the
    /// Charging Stations. So Charging Stations have to accept incoming HTTP
    /// Web Socket requests, which is a vendor extension.
    /// 
    /// Between the Charging Stations and the Local Controller the "normal"
    /// OCPP transport JSON array is used. Between the Local Controller and
    /// the Gateway and between the Gateway and the CSMS the OCPP Overlay
    /// Network transport is used.
    /// 
    /// [cs1] ←──\
    /// [cs2] ←──── [lc] ◄━━━ [gw] ◄━━━ [csms]
    /// [cs3] ←──/
    /// </summary>
    public abstract class AOCPPInfrastructureReversed
    {

        #region Data

        public DNSClient                    DNSClient;


        public TestChargingStation?         chargingStation1;
        public IPPort                       chargingStation1_tcpPort                    = IPPort.Parse(6001);
        public OCPPWebSocketServer?         chargingStation1_OCPPWebSocketServer;

        public TestChargingStation?         chargingStation2;
        public IPPort                       chargingStation2_tcpPort                    = IPPort.Parse(6002);
        public OCPPWebSocketServer?         chargingStation2_OCPPWebSocketServer;

        public TestChargingStation?         chargingStation3;
        public IPPort                       chargingStation3_tcpPort                    = IPPort.Parse(6003);
        public OCPPWebSocketServer?         chargingStation3_OCPPWebSocketServer;

        public TestLocalController?         ocppLocalController;
        public IPPort                       ocppLocalController_tcpPort                 = IPPort.Parse(6010);
        public OCPPWebSocketServer?         ocppLocalController_OCPPWebSocketServer;
        public protocols.WWCP.EnergyMeter?  upstreamEnergyMeter;

        public TestGateway?                 ocppGateway;
        public IPPort                       ocppGateway_tcpPort                         = IPPort.Parse(6020);
        public OCPPWebSocketServer?         ocppGateway_OCPPWebSocketServer;

        public TestCSMS2?                   csms;

        #endregion

        #region Constructor(s)

        public AOCPPInfrastructureReversed()
        {

            this.DNSClient           = new();

        }

        #endregion


        #region SetupOnce()

        [OneTimeSetUp]
        public async Task SetupOnce()
        {

            var notBefore = Timestamp.Now - TimeSpan.FromDays(1);
            var notAfter  = notBefore     + TimeSpan.FromDays(365);

            #region Setup chargingStation1

            chargingStation1                      = new TestChargingStation(

                                                        Id:                         NetworkingNode_Id.Parse("cs1"),
                                                        VendorName:                 "GraphDefined",
                                                        Model:                      "vcs1",
                                                        Description:                I18NString.Create(Languages.en, "The first charging station for testing"),
                                                        SerialNumber:               "cs#1",
                                                        FirmwareVersion:            "cs-fw v1.0",
                                                        Modem:                       new protocols.OCPPv2_1.Modem(
                                                                                         ICCID:       "ICCID#1",
                                                                                         IMSI:        "IMSI#1",
                                                                                         CustomData:   null
                                                                                     ),

                                                        SignaturePolicy:             null,
                                                        ForwardingSignaturePolicy:   null,

                                                        HTTPUploadPort:              null,
                                                        HTTPDownloadPort:            null,

                                                        DisableSendHeartbeats:       false,
                                                        SendHeartbeatsEvery:         null,
                                                        DefaultRequestTimeout:       null,

                                                        DisableMaintenanceTasks:     false,
                                                        MaintenanceEvery:            null,
                                                        DNSClient:                   DNSClient

                                                    );

            chargingStation1_OCPPWebSocketServer  = chargingStation1.AttachWebSocketServer(

                                                        HTTPServiceName:              null,
                                                        IPAddress:                    null,
                                                        TCPPort:                      chargingStation1_tcpPort,
                                                        Description:                  null,

                                                        RequireAuthentication:        true,
                                                        DisableWebSocketPings:        false,
                                                        WebSocketPingEvery:           null,
                                                        SlowNetworkSimulationDelay:   null,

                                                        ServerCertificateSelector:    null,
                                                        ClientCertificateValidator:   null,
                                                        LocalCertificateSelector:     null,
                                                        AllowedTLSProtocols:          null,
                                                        ClientCertificateRequired:    null,
                                                        CheckCertificateRevocation:   null,

                                                        ServerThreadNameCreator:      null,
                                                        ServerThreadPrioritySetter:   null,
                                                        ServerThreadIsBackground:     null,
                                                        ConnectionIdBuilder:          null,
                                                        ConnectionTimeout:            null,
                                                        MaxClientConnections:         null,

                                                        AutoStart:                    true

                                                    );

            #endregion

            #region Setup chargingStation2

            chargingStation2                      = new TestChargingStation(

                                                        Id:                         NetworkingNode_Id.Parse("cs2"),
                                                        VendorName:                 "GraphDefined",
                                                        Model:                      "vcs2",
                                                        Description:                I18NString.Create(Languages.en, "The second charging station for testing"),
                                                        SerialNumber:               "cs#2",
                                                        FirmwareVersion:            "cs-fw v2.0",
                                                        Modem:                       new protocols.OCPPv2_1.Modem(
                                                                                         ICCID:       "ICCID#2",
                                                                                         IMSI:        "IMSI#2",
                                                                                         CustomData:   null
                                                                                     ),

                                                        SignaturePolicy:             null,
                                                        ForwardingSignaturePolicy:   null,

                                                        HTTPUploadPort:              null,
                                                        HTTPDownloadPort:            null,

                                                        DisableSendHeartbeats:       false,
                                                        SendHeartbeatsEvery:         null,
                                                        DefaultRequestTimeout:       null,

                                                        DisableMaintenanceTasks:     false,
                                                        MaintenanceEvery:            null,
                                                        DNSClient:                   DNSClient

                                                    );

            chargingStation2_OCPPWebSocketServer  = chargingStation2.AttachWebSocketServer(

                                                        HTTPServiceName:              null,
                                                        IPAddress:                    null,
                                                        TCPPort:                      chargingStation2_tcpPort,
                                                        Description:                  null,

                                                        RequireAuthentication:        true,
                                                        DisableWebSocketPings:        false,
                                                        WebSocketPingEvery:           null,
                                                        SlowNetworkSimulationDelay:   null,

                                                        ServerCertificateSelector:    null,
                                                        ClientCertificateValidator:   null,
                                                        LocalCertificateSelector:     null,
                                                        AllowedTLSProtocols:          null,
                                                        ClientCertificateRequired:    null,
                                                        CheckCertificateRevocation:   null,

                                                        ServerThreadNameCreator:      null,
                                                        ServerThreadPrioritySetter:   null,
                                                        ServerThreadIsBackground:     null,
                                                        ConnectionIdBuilder:          null,
                                                        ConnectionTimeout:            null,
                                                        MaxClientConnections:         null,

                                                        AutoStart:                    true

                                                    );

            #endregion

            #region Setup chargingStation3

            chargingStation3                      = new TestChargingStation(

                                                        Id:                         NetworkingNode_Id.Parse("cs3"),
                                                        VendorName:                 "GraphDefined",
                                                        Model:                      "vcs3",
                                                        Description:                I18NString.Create(Languages.en, "The third charging station for testing"),
                                                        SerialNumber:               "cs#3",
                                                        FirmwareVersion:            "cs-fw v3.0",
                                                        Modem:                       new protocols.OCPPv2_1.Modem(
                                                                                         ICCID:       "ICCID#3",
                                                                                         IMSI:        "IMSI#3",
                                                                                         CustomData:   null
                                                                                     ),

                                                        SignaturePolicy:             null,
                                                        ForwardingSignaturePolicy:   null,

                                                        HTTPUploadPort:              null,
                                                        HTTPDownloadPort:            null,

                                                        DisableSendHeartbeats:       false,
                                                        SendHeartbeatsEvery:         null,
                                                        DefaultRequestTimeout:       null,

                                                        DisableMaintenanceTasks:     false,
                                                        MaintenanceEvery:            null,
                                                        DNSClient:                   DNSClient

                                                    );

            chargingStation3_OCPPWebSocketServer  = chargingStation3.AttachWebSocketServer(

                                                        HTTPServiceName:              null,
                                                        IPAddress:                    null,
                                                        TCPPort:                      chargingStation3_tcpPort,
                                                        Description:                  null,

                                                        RequireAuthentication:        true,
                                                        DisableWebSocketPings:        false,
                                                        WebSocketPingEvery:           null,
                                                        SlowNetworkSimulationDelay:   null,

                                                        ServerCertificateSelector:    null,
                                                        ClientCertificateValidator:   null,
                                                        LocalCertificateSelector:     null,
                                                        AllowedTLSProtocols:          null,
                                                        ClientCertificateRequired:    null,
                                                        CheckCertificateRevocation:   null,

                                                        ServerThreadNameCreator:      null,
                                                        ServerThreadPrioritySetter:   null,
                                                        ServerThreadIsBackground:     null,
                                                        ConnectionIdBuilder:          null,
                                                        ConnectionTimeout:            null,
                                                        MaxClientConnections:         null,

                                                        AutoStart:                    true

                                                    );

            #endregion


            #region Setup OCPP Local Controller

            ocppLocalController                      = new TestLocalController(

                                                           Id:                          NetworkingNode_Id.Parse("lc1"),
                                                           VendorName:                  "GraphDefined",
                                                           Model:                       "vlc1",
                                                           Description:                 I18NString.Create(Languages.en, "An OCPP Local Controller for testing"),
                                                           SerialNumber:                null,
                                                           FirmwareVersion:             null,
                                                           Modem:                       null,

                                                           SignaturePolicy:             null,
                                                           ForwardingSignaturePolicy:   null,

                                                           HTTPUploadPort:              null,
                                                           HTTPDownloadPort:            null,

                                                           DisableSendHeartbeats:       false,
                                                           SendHeartbeatsEvery:         null,
                                                           DefaultRequestTimeout:       null,

                                                           DisableMaintenanceTasks:     false,
                                                           MaintenanceEvery:            null,
                                                           DNSClient:                   DNSClient

                                                       );

            var cs1Auth                            = chargingStation1_OCPPWebSocketServer.AddOrUpdateHTTPBasicAuth(
                                                         ocppLocalController.Id,
                                                         "lc_cs1_12345678"
                                                     );

            var ocppLocalControllerConnectResult1  = await ocppLocalController.ConnectWebSocketClient(

                                                         NetworkingNodeId:             chargingStation1.Id,
                                                         RemoteURL:                    URL.Parse($"ws://127.0.0.1:{chargingStation1_tcpPort}"),
                                                         VirtualHostname:              null,
                                                         Description:                  null,
                                                         PreferIPv4:                   null,
                                                         RemoteCertificateValidator:   null,
                                                         LocalCertificateSelector:     null,
                                                         ClientCert:                   null,
                                                         TLSProtocol:                  null,
                                                         HTTPUserAgent:                null,
                                                         HTTPAuthentication:           cs1Auth,
                                                         RequestTimeout:               null,
                                                         TransmissionRetryDelay:       null,
                                                         MaxNumberOfRetries:           3,
                                                         InternalBufferSize:           null,

                                                         SecWebSocketProtocols:        null,
                                                         NetworkingMode:               protocols.OCPP.WebSockets.NetworkingMode.Standard,

                                                         DisableWebSocketPings:        false,
                                                         WebSocketPingEvery:           null,
                                                         SlowNetworkSimulationDelay:   null,

                                                         DisableMaintenanceTasks:      false,
                                                         MaintenanceEvery:             null,

                                                         LoggingPath:                  null,
                                                         LoggingContext:               String.Empty,
                                                         LogfileCreator:               null,
                                                         HTTPLogger:                   null,
                                                         DNSClient:                    null

                                                     );

            Assert.That(ocppLocalControllerConnectResult1.HTTPStatusCode.Code, Is.EqualTo(101), $"OCPP Local Controller could not connect to Charging Station #1: {ocppLocalControllerConnectResult1.HTTPStatusCode}");


            var cs2Auth                            = chargingStation2_OCPPWebSocketServer.AddOrUpdateHTTPBasicAuth(
                                                         ocppLocalController.Id,
                                                         "lc_cs2_12345678"
                                                     );

            var ocppLocalControllerConnectResult2  = await ocppLocalController.ConnectWebSocketClient(

                                                         NetworkingNodeId:             chargingStation2.Id,
                                                         RemoteURL:                    URL.Parse($"ws://127.0.0.1:{chargingStation2_tcpPort}"),
                                                         VirtualHostname:              null,
                                                         Description:                  null,
                                                         PreferIPv4:                   null,
                                                         RemoteCertificateValidator:   null,
                                                         LocalCertificateSelector:     null,
                                                         ClientCert:                   null,
                                                         TLSProtocol:                  null,
                                                         HTTPUserAgent:                null,
                                                         HTTPAuthentication:           cs2Auth,
                                                         RequestTimeout:               null,
                                                         TransmissionRetryDelay:       null,
                                                         MaxNumberOfRetries:           3,
                                                         InternalBufferSize:           null,

                                                         SecWebSocketProtocols:        null,
                                                         NetworkingMode:               protocols.OCPP.WebSockets.NetworkingMode.Standard,

                                                         DisableWebSocketPings:        false,
                                                         WebSocketPingEvery:           null,
                                                         SlowNetworkSimulationDelay:   null,

                                                         DisableMaintenanceTasks:      false,
                                                         MaintenanceEvery:             null,

                                                         LoggingPath:                  null,
                                                         LoggingContext:               String.Empty,
                                                         LogfileCreator:               null,
                                                         HTTPLogger:                   null,
                                                         DNSClient:                    null

                                                     );

            Assert.That(ocppLocalControllerConnectResult2.HTTPStatusCode.Code, Is.EqualTo(101), $"OCPP Local Controller could not connect to Charging Station #2: {ocppLocalControllerConnectResult2.HTTPStatusCode}");


            var cs3Auth                            = chargingStation3_OCPPWebSocketServer.AddOrUpdateHTTPBasicAuth(
                                                         ocppLocalController.Id,
                                                         "lc_cs3_12345678"
                                                     );

            var ocppLocalControllerConnectResult3  = await ocppLocalController.ConnectWebSocketClient(

                                                         NetworkingNodeId:             chargingStation3.Id,
                                                         RemoteURL:                    URL.Parse($"ws://127.0.0.1:{chargingStation3_tcpPort}"),
                                                         VirtualHostname:              null,
                                                         Description:                  null,
                                                         PreferIPv4:                   null,
                                                         RemoteCertificateValidator:   null,
                                                         LocalCertificateSelector:     null,
                                                         ClientCert:                   null,
                                                         TLSProtocol:                  null,
                                                         HTTPUserAgent:                null,
                                                         HTTPAuthentication:           cs3Auth,
                                                         RequestTimeout:               null,
                                                         TransmissionRetryDelay:       null,
                                                         MaxNumberOfRetries:           3,
                                                         InternalBufferSize:           null,

                                                         SecWebSocketProtocols:        null,
                                                         NetworkingMode:               protocols.OCPP.WebSockets.NetworkingMode.Standard,

                                                         DisableWebSocketPings:        false,
                                                         WebSocketPingEvery:           null,
                                                         SlowNetworkSimulationDelay:   null,

                                                         DisableMaintenanceTasks:      false,
                                                         MaintenanceEvery:             null,

                                                         LoggingPath:                  null,
                                                         LoggingContext:               String.Empty,
                                                         LogfileCreator:               null,
                                                         HTTPLogger:                   null,
                                                         DNSClient:                    null

                                                     );

            Assert.That(ocppLocalControllerConnectResult3.HTTPStatusCode.Code, Is.EqualTo(101), $"OCPP Local Controller could not connect to Charging Station #3: {ocppLocalControllerConnectResult3.HTTPStatusCode}");


            ocppLocalController_OCPPWebSocketServer  = ocppLocalController.AttachWebSocketServer(

                                                          HTTPServiceName:              null,
                                                          IPAddress:                    null,
                                                          TCPPort:                      ocppLocalController_tcpPort,
                                                          Description:                  null,

                                                          RequireAuthentication:        true,
                                                          DisableWebSocketPings:        false,
                                                          WebSocketPingEvery:           null,
                                                          SlowNetworkSimulationDelay:   null,

                                                          ServerCertificateSelector:    null,
                                                          ClientCertificateValidator:   null,
                                                          LocalCertificateSelector:     null,
                                                          AllowedTLSProtocols:          null,
                                                          ClientCertificateRequired:    null,
                                                          CheckCertificateRevocation:   null,

                                                          ServerThreadNameCreator:      null,
                                                          ServerThreadPrioritySetter:   null,
                                                          ServerThreadIsBackground:     null,
                                                          ConnectionIdBuilder:          null,
                                                          ConnectionTimeout:            null,
                                                          MaxClientConnections:         null,

                                                          AutoStart:                    true

                                                      );

            #endregion


            #region Setup OCPP Gateway

            ocppGateway                      = new TestGateway(

                                                   Id:                          NetworkingNode_Id.Parse("gw1"),
                                                   Description:                 I18NString.Create(Languages.en, "An OCPP Gateway for testing"),

                                                   SignaturePolicy:             null,
                                                   ForwardingSignaturePolicy:   null,

                                                   DisableSendHeartbeats:       false,
                                                   SendHeartbeatsEvery:         null,
                                                   DefaultRequestTimeout:       null,

                                                   DisableMaintenanceTasks:     false,
                                                   MaintenanceEvery:            null,
                                                   DNSClient:                   DNSClient

                                               );

            var ocppGatewayAuth              = ocppLocalController_OCPPWebSocketServer.AddOrUpdateHTTPBasicAuth(
                                                   ocppGateway.Id,
                                                   "gw12345678"
                                               );

            var ocppGatewayConnectResult     = await ocppGateway.ConnectWebSocketClient(

                                                   NetworkingNodeId:             ocppLocalController.Id,
                                                   RemoteURL:                    URL.Parse($"ws://127.0.0.1:{ocppLocalController_tcpPort}"),
                                                   VirtualHostname:              null,
                                                   Description:                  null,
                                                   PreferIPv4:                   null,
                                                   RemoteCertificateValidator:   null,
                                                   LocalCertificateSelector:     null,
                                                   ClientCert:                   null,
                                                   TLSProtocol:                  null,
                                                   HTTPUserAgent:                null,
                                                   HTTPAuthentication:           ocppGatewayAuth,
                                                   RequestTimeout:               null,
                                                   TransmissionRetryDelay:       null,
                                                   MaxNumberOfRetries:           3,
                                                   InternalBufferSize:           null,

                                                   SecWebSocketProtocols:        null,
                                                   NetworkingMode:               protocols.OCPP.WebSockets.NetworkingMode.OverlayNetwork,

                                                   DisableWebSocketPings:        false,
                                                   WebSocketPingEvery:           null,
                                                   SlowNetworkSimulationDelay:   null,

                                                   DisableMaintenanceTasks:      false,
                                                   MaintenanceEvery:             null,

                                                   LoggingPath:                  null,
                                                   LoggingContext:               String.Empty,
                                                   LogfileCreator:               null,
                                                   HTTPLogger:                   null,
                                                   DNSClient:                    null

                                               );

            Assert.That(ocppGatewayConnectResult.HTTPStatusCode.Code, Is.EqualTo(101), $"OCPP Gateway could not connect to the OCPP Local Controller: {ocppGatewayConnectResult.HTTPStatusCode}");


            ocppGateway_OCPPWebSocketServer  = ocppGateway.AttachWebSocketServer(

                                                   HTTPServiceName:              null,
                                                   IPAddress:                    null,
                                                   TCPPort:                      ocppGateway_tcpPort,
                                                   Description:                  null,

                                                   RequireAuthentication:        true,
                                                   DisableWebSocketPings:        false,
                                                   WebSocketPingEvery:           null,
                                                   SlowNetworkSimulationDelay:   null,

                                                   ServerCertificateSelector:    null,
                                                   ClientCertificateValidator:   null,
                                                   LocalCertificateSelector:     null,
                                                   AllowedTLSProtocols:          null,
                                                   ClientCertificateRequired:    null,
                                                   CheckCertificateRevocation:   null,

                                                   ServerThreadNameCreator:      null,
                                                   ServerThreadPrioritySetter:   null,
                                                   ServerThreadIsBackground:     null,
                                                   ConnectionIdBuilder:          null,
                                                   ConnectionTimeout:            null,
                                                   MaxClientConnections:         null,

                                                   AutoStart:                    true

                                               );

            #endregion


            #region Setup Charging Station Management System

            csms = new TestCSMS2(

                       Id:                          NetworkingNode_Id.Parse("csms1"),
                       Description:                 I18NString.Create(Languages.en, "A Charging Station Management System for testing"),

                       SignaturePolicy:             null,
                       ForwardingSignaturePolicy:   null,

                       HTTPUploadPort:              null,
                       HTTPDownloadPort:            null,

                       DisableSendHeartbeats:       false,
                       SendHeartbeatsEvery:         null,
                       DefaultRequestTimeout:       null,

                       DisableMaintenanceTasks:     false,
                       MaintenanceEvery:            null,
                       DNSClient:                   DNSClient

                   );


            var csmsAuth           = ocppGateway_OCPPWebSocketServer.AddOrUpdateHTTPBasicAuth(
                                         csms.Id,
                                         "gw12345678"
                                     );

            var csmsConnectResult  = await csms.ConnectWebSocketClient(

                                         NetworkingNodeId:             ocppGateway.Id,
                                         RemoteURL:                    URL.Parse($"ws://127.0.0.1:{ocppGateway_tcpPort}"),
                                         VirtualHostname:              null,
                                         Description:                  null,
                                         PreferIPv4:                   null,
                                         RemoteCertificateValidator:   null,
                                         LocalCertificateSelector:     null,
                                         ClientCert:                   null,
                                         TLSProtocol:                  null,
                                         HTTPUserAgent:                null,
                                         HTTPAuthentication:           csmsAuth,
                                         RequestTimeout:               null,
                                         TransmissionRetryDelay:       null,
                                         MaxNumberOfRetries:           3,
                                         InternalBufferSize:           null,

                                         SecWebSocketProtocols:        null,
                                         NetworkingMode:               protocols.OCPP.WebSockets.NetworkingMode.OverlayNetwork,

                                         DisableWebSocketPings:        false,
                                         WebSocketPingEvery:           null,
                                         SlowNetworkSimulationDelay:   null,

                                         DisableMaintenanceTasks:      false,
                                         MaintenanceEvery:             null,

                                         LoggingPath:                  null,
                                         LoggingContext:               String.Empty,
                                         LogfileCreator:               null,
                                         HTTPLogger:                   null,
                                         DNSClient:                    null

                                     );

            Assert.That(csmsConnectResult.HTTPStatusCode.Code, Is.EqualTo(101), $"CSMS could not connect to the OCPP Gateway: {csmsConnectResult.HTTPStatusCode}");

            #endregion


            //ToDo: Make use of the routing protocol vendor extensions!

            // Towards the CSMS
            chargingStation1.   OCPP.AddStaticRouting(NetworkingNode_Id.CSMS,  ocppLocalController.Id);
            ocppLocalController.OCPP.AddStaticRouting(NetworkingNode_Id.CSMS,  ocppGateway.Id);
            ocppGateway.        OCPP.AddStaticRouting(NetworkingNode_Id.CSMS,  csms.Id);

            // Towards the charging stations
            csms.               OCPP.AddStaticRouting(ocppLocalController.Id,  ocppGateway.Id);
            csms.               OCPP.AddStaticRouting(chargingStation1.Id,     ocppGateway.Id);
            csms.               OCPP.AddStaticRouting(chargingStation2.Id,     ocppGateway.Id);
            csms.               OCPP.AddStaticRouting(chargingStation3.Id,     ocppGateway.Id);

            ocppGateway.        OCPP.AddStaticRouting(chargingStation1.Id,     ocppLocalController.Id);
            ocppGateway.        OCPP.AddStaticRouting(chargingStation2.Id,     ocppLocalController.Id);
            ocppGateway.        OCPP.AddStaticRouting(chargingStation3.Id,     ocppLocalController.Id);

        }

        #endregion

        #region SetupEachTest()

        [SetUp]
        public async Task SetupEachTest()
        {

            Timestamp.Reset();

        }

        #endregion

        #region ShutdownEachTest()

        [TearDown]
        public void ShutdownEachTest()
        {

        }

        #endregion

        #region ShutdownOnce()

        [OneTimeTearDown]
        public void ShutdownOnce()
        {

        }

        #endregion


    }

}