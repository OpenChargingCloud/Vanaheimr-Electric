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

using cloud.charging.open.protocols.OCPPv2_1;
using cloud.charging.open.protocols.OCPPv2_1.CS;
using cloud.charging.open.protocols.OCPPv2_1.CSMS;
using cloud.charging.open.protocols.OCPPv2_1.Gateway;
using cloud.charging.open.protocols.OCPPv2_1.NetworkingNode;
using cloud.charging.open.protocols.OCPPv2_1.LocalController;
using cloud.charging.open.protocols.OCPP;

#endregion

namespace cloud.charging.open.vanaheimr.electric.UnitTests.OverlayNetwork
{

    /// <summary>
    /// Charging infrastructure test defaults using an OCPP Overlay Network
    /// consisting of a CSMS, an OCPP Gateway, an OCPP Local Controller and
    /// three Charging Stations.
    /// 
    /// The HTTP Web Socket connections are initiated in "normal order" from
    /// the Charging Stations to the Local Controller, to the Gateway and
    /// finally to the CSMS.
    /// 
    /// Between the Charging Stations and the Local Controller the "normal"
    /// OCPP transport JSON array is used. Between the Local Controller and
    /// the Gateway and between the Gateway and the CSMS the OCPP Overlay
    /// Network transport is used.
    /// 
    /// [cs1] ──⭨
    /// [cs2] ───→ [lc] ━━━► [gw] ━━━► [csms]
    /// [cs3] ──🡕
    /// </summary>
    public abstract class AOverlayNetwork
    {

        #region Data

        public TestCSMS?                    csms;
        public IPPort                       csms_tcpPort                                = IPPort.Parse(5000);
        public OCPPWebSocketServer?         csms_OCPPWebSocketServer;
        public KeyPair?                     csms_keyPair;

        public TestGateway?                 ocppGateway;
        public IPPort                       ocppGateway_tcpPort                         = IPPort.Parse(5010);
        public OCPPWebSocketServer?         ocppGateway_OCPPWebSocketServer;
        public KeyPair?                     ocppGateway_keyPair;

        public TestLocalController?         ocppLocalController;
        public IPPort                       ocppLocalController_tcpPort                 = IPPort.Parse(5020);
        public OCPPWebSocketServer?         ocppLocalController_OCPPWebSocketServer;
        public KeyPair?                     ocppLocalController_keyPair;
        public protocols.WWCP.EnergyMeter?  upstreamEnergyMeter;

        public TestChargingStation?         chargingStation1;
        public KeyPair?                     chargingStation1_keyPair;

        public TestChargingStation?         chargingStation2;
        public KeyPair?                     chargingStation2_keyPair;

        public TestChargingStation?         chargingStation3;
        public KeyPair?                     chargingStation3_keyPair;

        public DNSClient                    DNSClient;

        #endregion

        #region Constructor(s)

        public AOverlayNetwork()
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


            #region Setup Charging Station Management System

            csms = new TestCSMS(

                       Id:                          NetworkingNode_Id.Parse("csms"),
                       VendorName:                  "GraphDefined",
                       Model:                       "vcsms1",
                       Description:                 I18NString.Create(Languages.en, "A Charging Station Management System for testing"),

                       SignaturePolicy:             null,
                       ForwardingSignaturePolicy:   null,

                       HTTPUploadPort:              null,
                       HTTPDownloadPort:            null,

                       DisableSendHeartbeats:       true,
                       SendHeartbeatsEvery:         null,
                       DefaultRequestTimeout:       null,

                       DisableMaintenanceTasks:     false,
                       MaintenanceEvery:            null,
                       DNSClient:                   DNSClient

                   );

            csms_OCPPWebSocketServer = csms.AttachWebSocketServer(

                                           HTTPServiceName:              null,
                                           IPAddress:                    null,
                                           TCPPort:                      csms_tcpPort,
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

            #region Define signature policy

            csms_keyPair = KeyPair.GenerateKeys()!;

            csms.OCPP.SignaturePolicy.AddSigningRule     (JSONContext.OCPP.Any,
                                                          KeyPair:                csms_keyPair!,
                                                          UserIdGenerator:        (signableMessage) => "cs001",
                                                          DescriptionGenerator:   (signableMessage) => I18NString.Create("Just a csms!"),
                                                          TimestampGenerator:     (signableMessage) => Timestamp.Now);

            csms.OCPP.SignaturePolicy.AddVerificationRule(JSONContext.OCPP.Any,
                                                          VerificationRuleActions.VerifyAll);

            #endregion

            #endregion


            #region Setup OCPP Gateway

            ocppGateway                      = new TestGateway(

                                                   Id:                          NetworkingNode_Id.Parse("gw"),
                                                   VendorName:                  "GraphDefined",
                                                   Model:                       "vgw1",
                                                   Description:                 I18NString.Create(Languages.en, "An OCPP Gateway for testing"),

                                                   SignaturePolicy:             null,
                                                   ForwardingSignaturePolicy:   null,

                                                   DisableSendHeartbeats:       true,
                                                   SendHeartbeatsEvery:         null,
                                                   DefaultRequestTimeout:       null,

                                                   DisableMaintenanceTasks:     false,
                                                   MaintenanceEvery:            null,
                                                   DNSClient:                   DNSClient

                                               );

            var ocppGatewayAuth              = csms_OCPPWebSocketServer.AddOrUpdateHTTPBasicAuth(
                                                                            ocppGateway.Id,
                                                                            "gw12345678"
                                                                        );

            var ocppGatewayConnectResult     = await ocppGateway.ConnectWebSocketClient(

                                                   NetworkingNodeId:             NetworkingNode_Id.CSMS,
                                                   RemoteURL:                    URL.Parse($"ws://127.0.0.1:{csms_tcpPort}"),
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
                                                   NetworkingMode:               NetworkingMode.OverlayNetwork,

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

            Assert.That(ocppGatewayConnectResult.HTTPStatusCode.Code, Is.EqualTo(101), $"OCPP Gateway could not connect to CSMS: {ocppGatewayConnectResult.HTTPStatusCode}");


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

            #region Define signature policy

            ocppGateway_keyPair = KeyPair.GenerateKeys()!;

            ocppGateway.OCPP.SignaturePolicy.AddSigningRule     (JSONContext.OCPP.Any,
                                                                 KeyPair:                ocppGateway_keyPair!,
                                                                 UserIdGenerator:        (signableMessage) => "gw001",
                                                                 DescriptionGenerator:   (signableMessage) => I18NString.Create("Just an OCPP gateway!"),
                                                                 TimestampGenerator:     (signableMessage) => Timestamp.Now);

            ocppGateway.OCPP.SignaturePolicy.AddVerificationRule(JSONContext.OCPP.Any,
                                                                 VerificationRuleActions.VerifyAll);

            #endregion

            #endregion


            #region Setup OCPP Local Controller

            ocppLocalController                      = new TestLocalController(

                                                           Id:                          NetworkingNode_Id.Parse("lc"),
                                                           VendorName:                  "GraphDefined",
                                                           Model:                       "vlc1",
                                                           SerialNumber:                null,
                                                           SoftwareVersion:             null,
                                                           Modem:                       null,
                                                           Description:                 I18NString.Create(Languages.en, "An OCPP Local Controller for testing"),

                                                           SignaturePolicy:             null,
                                                           ForwardingSignaturePolicy:   null,

                                                           HTTPUploadPort:              null,
                                                           HTTPDownloadPort:            null,

                                                           DisableSendHeartbeats:       true,
                                                           SendHeartbeatsEvery:         null,
                                                           DefaultRequestTimeout:       null,

                                                           DisableMaintenanceTasks:     false,
                                                           MaintenanceEvery:            null,
                                                           DNSClient:                   DNSClient

                                                       );

            var ocppLocalControllerAuth              = ocppGateway_OCPPWebSocketServer.AddOrUpdateHTTPBasicAuth(
                                                                                           ocppLocalController.Id,
                                                                                           "lc12345678"
                                                                                       );

            var ocppLocalControllerConnectResult     = await ocppLocalController.ConnectWebSocketClient(

                                                                 NetworkingNodeId:             NetworkingNode_Id.CSMS,
                                                                 RemoteURL:                    URL.Parse($"ws://127.0.0.1:{ocppGateway_tcpPort}"),
                                                                 VirtualHostname:              null,
                                                                 Description:                  null,
                                                                 PreferIPv4:                   null,
                                                                 RemoteCertificateValidator:   null,
                                                                 LocalCertificateSelector:     null,
                                                                 ClientCert:                   null,
                                                                 TLSProtocol:                  null,
                                                                 HTTPUserAgent:                null,
                                                                 HTTPAuthentication:           ocppLocalControllerAuth,
                                                                 RequestTimeout:               null,
                                                                 TransmissionRetryDelay:       null,
                                                                 MaxNumberOfRetries:           3,
                                                                 InternalBufferSize:           null,

                                                                 SecWebSocketProtocols:        null,
                                                                 NetworkingMode:               NetworkingMode.OverlayNetwork,

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

            Assert.That(ocppLocalControllerConnectResult.HTTPStatusCode.Code, Is.EqualTo(101), $"OCPP Local Controller could not connect to OCPP Gateway: {ocppLocalControllerConnectResult.HTTPStatusCode}");


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

            #region Define signature policy

            ocppLocalController_keyPair = KeyPair.GenerateKeys()!;

            ocppLocalController.OCPP.SignaturePolicy.AddSigningRule     (JSONContext.OCPP.Any,
                                                                         KeyPair:                ocppLocalController_keyPair!,
                                                                         UserIdGenerator:        (signableMessage) => "lc001",
                                                                         DescriptionGenerator:   (signableMessage) => I18NString.Create("Just an OCPP local controller!"),
                                                                         TimestampGenerator:     (signableMessage) => Timestamp.Now);

            ocppLocalController.OCPP.SignaturePolicy.AddVerificationRule(JSONContext.OCPP.Any,
                                                                         VerificationRuleActions.VerifyAll);

            #endregion

            #endregion


            #region Setup chargingStation1

            chargingStation1      = new TestChargingStation(

                                        Id:                         NetworkingNode_Id.Parse("cs1"),
                                        VendorName:                 "GraphDefined",
                                        Model:                      "vcs1",
                                        Description:                I18NString.Create(Languages.en, "The first charging station for testing"),
                                        SerialNumber:               "cs#1",
                                        FirmwareVersion:            "cs-fw v1.0",
                                        Modem:                       new Modem(
                                                                         ICCID:       "ICCID#1",
                                                                         IMSI:        "IMSI#1",
                                                                         CustomData:   null
                                                                     ),

                                        SignaturePolicy:             null,
                                        ForwardingSignaturePolicy:   null,

                                        HTTPUploadPort:              null,
                                        HTTPDownloadPort:            null,

                                        DisableSendHeartbeats:       true,
                                        SendHeartbeatsEvery:         null,
                                        DefaultRequestTimeout:       null,

                                        DisableMaintenanceTasks:     false,
                                        MaintenanceEvery:            null,
                                        DNSClient:                   DNSClient

                                    );

            var cs1Auth           = ocppLocalController_OCPPWebSocketServer.AddOrUpdateHTTPBasicAuth(
                                                                                chargingStation1.Id,
                                                                                "cs1_12345678"
                                                                            );

            var cs1ConnectResult  = await chargingStation1.ConnectWebSocketClient(

                                              NetworkingNodeId:             NetworkingNode_Id.CSMS,
                                              RemoteURL:                    URL.Parse($"ws://127.0.0.1:{ocppLocalController_tcpPort}"),
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
                                              NetworkingMode:               null,

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

            Assert.That(cs1ConnectResult.HTTPStatusCode.Code, Is.EqualTo(101), $"Charging Station #1 could not connect to OCPP Local Controller: {cs1ConnectResult.HTTPStatusCode}");

            #region Define signature policy

            chargingStation1_keyPair = KeyPair.GenerateKeys()!;

            chargingStation1.OCPP.SignaturePolicy.AddSigningRule     (JSONContext.OCPP.Any,
                                                                      KeyPair:                chargingStation1_keyPair!,
                                                                      UserIdGenerator:        (signableMessage) => "cs001",
                                                                      DescriptionGenerator:   (signableMessage) => I18NString.Create("Just the 1st charging station!"),
                                                                      TimestampGenerator:     (signableMessage) => Timestamp.Now);

            chargingStation1.OCPP.SignaturePolicy.AddVerificationRule(JSONContext.OCPP.Any,
                                                                      VerificationRuleActions.VerifyAll);

            #endregion

            #endregion

            #region Setup chargingStation2

            chargingStation2      = new TestChargingStation(

                                            Id:                         NetworkingNode_Id.Parse("cs2"),
                                            VendorName:                 "GraphDefined",
                                            Model:                      "vcs2",
                                            Description:                I18NString.Create(Languages.en, "The second charging station for testing"),
                                            SerialNumber:               "cs#2",
                                            FirmwareVersion:            "cs-fw v2.0",
                                            Modem:                       new Modem(
                                                                             ICCID:       "ICCID#2",
                                                                             IMSI:        "IMSI#2",
                                                                             CustomData:   null
                                                                         ),

                                            SignaturePolicy:             null,
                                            ForwardingSignaturePolicy:   null,

                                            HTTPUploadPort:              null,
                                            HTTPDownloadPort:            null,

                                            DisableSendHeartbeats:       true,
                                            SendHeartbeatsEvery:         null,
                                            DefaultRequestTimeout:       null,

                                            DisableMaintenanceTasks:     false,
                                            MaintenanceEvery:            null,
                                            DNSClient:                   DNSClient

                                        );

            var cs2Auth           = ocppLocalController_OCPPWebSocketServer.AddOrUpdateHTTPBasicAuth(
                                                                                chargingStation2.Id,
                                                                                "cs2_12345678"
                                                                            );

            var cs2ConnectResult  = await chargingStation2.ConnectWebSocketClient(

                                              NetworkingNodeId:             NetworkingNode_Id.CSMS,
                                              RemoteURL:                    URL.Parse($"ws://127.0.0.1:{ocppLocalController_tcpPort}"),
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
                                              NetworkingMode:               null,

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

            Assert.That(cs2ConnectResult.HTTPStatusCode.Code, Is.EqualTo(101), $"Charging Station #2 could not connect to OCPP Local Controller: {cs2ConnectResult.HTTPStatusCode}");

            #region Define signature policy

            chargingStation2_keyPair = KeyPair.GenerateKeys()!;

            chargingStation2.OCPP.SignaturePolicy.AddSigningRule     (JSONContext.OCPP.Any,
                                                                      KeyPair:                chargingStation2_keyPair!,
                                                                      UserIdGenerator:        (signableMessage) => "cs002",
                                                                      DescriptionGenerator:   (signableMessage) => I18NString.Create("Just the 2st charging station!"),
                                                                      TimestampGenerator:     (signableMessage) => Timestamp.Now);

            chargingStation2.OCPP.SignaturePolicy.AddVerificationRule(JSONContext.OCPP.Any,
                                                                      VerificationRuleActions.VerifyAll);

            #endregion

            #endregion

            #region Setup chargingStation3

            chargingStation3      = new TestChargingStation(

                                            Id:                         NetworkingNode_Id.Parse("cs3"),
                                            VendorName:                 "GraphDefined",
                                            Model:                      "vcs3",
                                            Description:                I18NString.Create(Languages.en, "The third charging station for testing"),
                                            SerialNumber:               "cs#3",
                                            FirmwareVersion:            "cs-fw v3.0",
                                            Modem:                       new Modem(
                                                                             ICCID:       "ICCID#3",
                                                                             IMSI:        "IMSI#3",
                                                                             CustomData:   null
                                                                         ),

                                            SignaturePolicy:             null,
                                            ForwardingSignaturePolicy:   null,

                                            HTTPUploadPort:              null,
                                            HTTPDownloadPort:            null,

                                            DisableSendHeartbeats:       true,
                                            SendHeartbeatsEvery:         null,
                                            DefaultRequestTimeout:       null,

                                            DisableMaintenanceTasks:     false,
                                            MaintenanceEvery:            null,
                                            DNSClient:                   DNSClient

                                        );

            var cs3Auth           = ocppLocalController_OCPPWebSocketServer.AddOrUpdateHTTPBasicAuth(
                                                                                chargingStation3.Id,
                                                                                "cs3_12345678"
                                                                            );

            var cs3ConnectResult  = await chargingStation3.ConnectWebSocketClient(

                                              NetworkingNodeId:             NetworkingNode_Id.CSMS,
                                              RemoteURL:                    URL.Parse($"ws://127.0.0.1:{ocppLocalController_tcpPort}"),
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
                                              NetworkingMode:               null,

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

            Assert.That(cs3ConnectResult.HTTPStatusCode.Code, Is.EqualTo(101), $"Charging Station #3 could not connect to OCPP Local Controller: {cs3ConnectResult.HTTPStatusCode}");

            #region Define signature policy

            chargingStation3_keyPair = KeyPair.GenerateKeys()!;

            chargingStation3.OCPP.SignaturePolicy.AddSigningRule     (JSONContext.OCPP.Any,
                                                                      KeyPair:                chargingStation3_keyPair!,
                                                                      UserIdGenerator:        (signableMessage) => "cs003",
                                                                      DescriptionGenerator:   (signableMessage) => I18NString.Create("Just the 3st charging station!"),
                                                                      TimestampGenerator:     (signableMessage) => Timestamp.Now);

            chargingStation3.OCPP.SignaturePolicy.AddVerificationRule(JSONContext.OCPP.Any,
                                                                      VerificationRuleActions.VerifyAll);

            #endregion

            #endregion


            //ToDo: Make use of the routing protocol vendor extensions!

            csms.       OCPP.AddStaticRouting(ocppLocalController.Id,  ocppGateway.Id);
            csms.       OCPP.AddStaticRouting(chargingStation1.Id,     ocppGateway.Id);
            csms.       OCPP.AddStaticRouting(chargingStation2.Id,     ocppGateway.Id);
            csms.       OCPP.AddStaticRouting(chargingStation3.Id,     ocppGateway.Id);

            ocppGateway.OCPP.AddStaticRouting(chargingStation1.Id,     ocppLocalController.Id);
            ocppGateway.OCPP.AddStaticRouting(chargingStation2.Id,     ocppLocalController.Id);
            ocppGateway.OCPP.AddStaticRouting(chargingStation3.Id,     ocppLocalController.Id);

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