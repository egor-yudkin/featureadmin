﻿using System;
using Akka.Actor;
using Akka.Event;
using FeatureAdmin.Core.Messages.Request;
using FeatureAdmin.Core.Models;
using FeatureAdmin.Core.Services;
using FeatureAdmin.Messages;

namespace FeatureAdmin.Actors
{
    /// <summary>
    /// class to convert a SharePoint location and its children to SPLocation objects
    /// </summary>
    public class LocationActor : BaseActor
    {
        private readonly ILoggingAdapter _log = Logging.GetLogger(Context);
        private readonly IDataService dataService;
        public LocationActor(IDataService dataService)
        {
            this.dataService = dataService;

            Receive<LoadChildLocationQuery>(message => ReceiveLoadChildLocationQuery(message));
            Receive<FeatureToggleRequest>(message => ReceiveFeatureToggleRequest(message));
        }

        /// <summary>
        /// Props provider
        /// </summary>
        /// <param name="dataService">SharePoint data service</param>
        /// <returns></returns>
        /// <remarks>
        /// see also https://getakka.net/articles/actors/receive-actor-api.html
        /// </remarks>
        public static Props Props(IDataService dataService)
        {
            return Akka.Actor.Props.Create(() => new LocationActor(
                   dataService));
        }

        protected override void ReceiveCancelMessage(CancelMessage message)
        {
            CancelationMessage = message.CancelationMessage;
        }

        /// <summary>
        /// at this time, there is always only one feature to be activated or deactivated in a location with same scope
        /// </summary>
        /// <param name="message"></param>
        private void ReceiveFeatureToggleRequest(FeatureToggleRequest message)
        {
            _log.Debug("Entered LocationActor-HandleFeatureToggleRequest");

            if (!TaskCanceled)
            {
                try
                {
                    switch (message.Action)
                    {
                        case Core.Models.Enums.FeatureAction.Activate:
                            HandleActivation(message);
                            break;
                        case Core.Models.Enums.FeatureAction.Deactivate:
                            HandleDeactivation(message);
                            break;
                        case Core.Models.Enums.FeatureAction.Upgrade:
                            HandleUpgrade(message);
                            break;
                        default:
                            throw new NotImplementedException("this action verb is not known to location actor for task id " + message.TaskId);
                    }
                }
                catch (Exception ex)
                {
                    var cancelationMsg = new CancelMessage(
                                message.TaskId,
                                ex.Message,
                                true,
                                ex
                                );

                    Sender.Tell(cancelationMsg);
                }
            }
        }

        private void HandleUpgrade(FeatureToggleRequest message)
        {
            string errorMsg = null;

            ActivatedFeature af;

            switch (message.Location.Scope)
            {
                case Core.Models.Enums.Scope.Web:
                    errorMsg += dataService.WebFeatureAction(
                        message.FeatureDefinition,
                        message.Location,
                        Core.Models.Enums.FeatureAction.Upgrade,
                        message.ElevatedPrivileges.Value,
                        message.Force.Value,
                        out af);
                    break;
                case Core.Models.Enums.Scope.Site:
                    errorMsg += dataService.SiteFeatureAction(
                        message.FeatureDefinition,
                        message.Location,
                        Core.Models.Enums.FeatureAction.Upgrade,
                        message.ElevatedPrivileges.Value,
                        message.Force.Value,
                        out af);
                    break;
                case Core.Models.Enums.Scope.WebApplication:
                    errorMsg += dataService.WebAppFeatureAction(
                        message.FeatureDefinition,
                        message.Location,
                        Core.Models.Enums.FeatureAction.Upgrade,
                        message.Force.Value,
                        out af);
                    break;
                case Core.Models.Enums.Scope.Farm:
                    errorMsg += dataService.FarmFeatureAction(
                        message.FeatureDefinition,
                        message.Location,
                        Core.Models.Enums.FeatureAction.Upgrade,
                        message.Force.Value,
                        out af);
                    break;
                case Core.Models.Enums.Scope.ScopeInvalid:
                    errorMsg += string.Format("Location '{0}' has invalid scope - not supported for feature upgrade.", message.Location.Id);
                    af = null;
                    break;
                default:
                    errorMsg += string.Format("Location '{0}' has unidentified scope - not supported for feature upgrade.", message.Location.Id);
                    af = null;
                    break;
            }


            if (string.IsNullOrEmpty(errorMsg))
            {
                var completed = new Core.Messages.Completed.FeatureUpgradeCompleted(
           message.TaskId,
           message.Location.UniqueId,
           af
           );

                Sender.Tell(completed);
            }
            else
            {
                var cancelationMsg = new CancelMessage(
                    message.TaskId,
                    errorMsg,
                    true
                    );

                Sender.Tell(cancelationMsg);
            }
        }

        private void HandleDeactivation(FeatureToggleRequest message)
        {
            string errorMsg = null;

            switch (message.Location.Scope)
            {
                case Core.Models.Enums.Scope.Web:
                    errorMsg += dataService.DeactivateWebFeature(
                        message.FeatureDefinition,
                        message.Location,
                        message.ElevatedPrivileges.Value,
                        message.Force.Value);
                    break;
                case Core.Models.Enums.Scope.Site:
                    errorMsg += dataService.DeactivateSiteFeature(
                        message.FeatureDefinition,
                        message.Location,
                        message.ElevatedPrivileges.Value,
                        message.Force.Value
                        );
                    break;
                case Core.Models.Enums.Scope.WebApplication:
                    errorMsg += dataService.DeactivateWebAppFeature(
                        message.FeatureDefinition,
                        message.Location,
                        message.Force.Value
                        );
                    break;
                case Core.Models.Enums.Scope.Farm:
                    errorMsg += dataService.DeactivateFarmFeature(
                        message.FeatureDefinition,
                        message.Location,
                        message.Force.Value
                        );
                    break;
                case Core.Models.Enums.Scope.ScopeInvalid:
                    errorMsg += string.Format("Location '{0}' has invalid scope - not supported for feature deactivation.", message.Location.Id);
                    break;
                default:
                    errorMsg += string.Format("Location '{0}' has unidentified scope - not supported for feature deactivation.", message.Location.Id);
                    break;
            }

            if (string.IsNullOrEmpty(errorMsg))
            {
                var completed = new Core.Messages.Completed.FeatureDeactivationCompleted(
                    message.TaskId,
                    message.Location.UniqueId,
                    message.FeatureDefinition.UniqueIdentifier
           );

                Sender.Tell(completed);
            }
            else
            {
                var cancelationMsg = new CancelMessage(
                    message.TaskId,
                    errorMsg,
                    true
                    );

                Sender.Tell(cancelationMsg);
            }
        }


        private void HandleActivation(FeatureToggleRequest message)
        {
            string errorMsg = null;


            ActivatedFeature af;

            switch (message.Location.Scope)
            {
                case Core.Models.Enums.Scope.Web:
                    errorMsg += dataService.WebFeatureAction(
                        message.FeatureDefinition,
                        message.Location,
                        Core.Models.Enums.FeatureAction.Activate,
                        message.ElevatedPrivileges.Value,
                        message.Force.Value,
                        out af);
                    break;
                case Core.Models.Enums.Scope.Site:
                    errorMsg += dataService.SiteFeatureAction(
                        message.FeatureDefinition,
                        message.Location,
                        Core.Models.Enums.FeatureAction.Activate,
                        message.ElevatedPrivileges.Value,
                        message.Force.Value,
                        out af);
                    break;
                case Core.Models.Enums.Scope.WebApplication:
                    errorMsg += dataService.WebAppFeatureAction(
                        message.FeatureDefinition,
                        message.Location,
                        Core.Models.Enums.FeatureAction.Activate,
                        message.Force.Value,
                        out af);
                    break;
                case Core.Models.Enums.Scope.Farm:
                    errorMsg += dataService.FarmFeatureAction(
                        message.FeatureDefinition,
                        message.Location,
                        Core.Models.Enums.FeatureAction.Activate,
                        message.Force.Value,
                        out af);
                    break;
                case Core.Models.Enums.Scope.ScopeInvalid:
                    errorMsg += string.Format("Location '{0}' has invalid scope - not supported for feature activation.", message.Location.Id);
                    af = null;
                    break;
                default:
                    errorMsg += string.Format("Location '{0}' has unidentified scope - not supported for feature activation.", message.Location.Id);
                    af = null;
                    break;
            }


            if (string.IsNullOrEmpty(errorMsg))
            {
                var completed = new Core.Messages.Completed.FeatureActivationCompleted(
           message.TaskId,
           message.Location.UniqueId,
           af
           );

                Sender.Tell(completed);
            }
            else
            {
                var cancelationMsg = new CancelMessage(
                    message.TaskId,
                    errorMsg,
                    true
                    );

                Sender.Tell(cancelationMsg);
            }
        }

        private void ReceiveLoadChildLocationQuery(LoadChildLocationQuery message)
        {
            _log.Debug("Entered LocationActor-LookupLocationHandlyLoadLocationQuery");

            if (!TaskCanceled)
            {
                try
                {
                    var location = message.Location;

                    if (location == null)
                    {
                        var loadedFarm = dataService.LoadFarm();
                        Sender.Tell(loadedFarm);
                    }
                    else
                    {
                        if (location.Scope == Core.Models.Enums.Scope.Farm)
                        {
                            var loadedWebApps = dataService.LoadWebApps();
                            Sender.Tell(loadedWebApps);
                        }
                        else
                        {
                            var loadedChildren = dataService.LoadWebAppChildren(location, message.ElevatedPrivileges);
                            Sender.Tell(loadedChildren);
                        }
                    }
                }
                catch (Exception ex)
                {
                    string tip = message.ElevatedPrivileges ? string.Empty : " Please try to check the option 'Elevated Privileges'.";


                    var cancelationMsg = new CancelMessage(
                                                message.TaskId,
                                                "Error loading farm hierarchy." + tip,
                                                true,
                                                ex
                                                );

                    Sender.Tell(cancelationMsg);
                }
            }
        }
    }
}
