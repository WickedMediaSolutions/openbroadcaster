using System;
using System.Collections.ObjectModel;
using System.Linq;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Audio;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.ViewModels;
using Xunit;

namespace OpenBroadcaster.Tests
{
    public sealed class SettingsViewModelTests
    {
        private static SettingsViewModel CreateViewModel(AppSettings settings)
        {
            settings.Automation ??= new AutomationSettings();
            settings.Automation.SimpleRotations ??= new ObservableCollection<SimpleRotation>();
            settings.Automation.SimpleSchedule ??= new ObservableCollection<SimpleSchedulerEntry>();

            var autoDj = new AutoDjSettingsService(loadFromDisk: false)
            {
                Rotations = settings.Automation.SimpleRotations.ToList(),
                Schedule = settings.Automation.SimpleSchedule.ToList(),
                DefaultRotationName = settings.Automation.DefaultRotationName ?? string.Empty
            };

            return new SettingsViewModel(settings, autoDjSettingsService: autoDj)
            {
                RotationDialogInvoker = (_, __, ___) => true,
                SchedulerDialogInvoker = (_, __) => true
            };
        }

        [Fact]
        public void Apply_RaisesSettingsChangedWithClonedPayload()
        {
            var settings = new AppSettings();
            var playback = new[] { new AudioDeviceInfo(0, "Program Out") };
            var input = new[] { new AudioDeviceInfo(1, "Studio Mic") };
            var vm = new SettingsViewModel(settings, playback, input, autoDjSettingsService: new AutoDjSettingsService(loadFromDisk: false));
            AppSettings? applied = null;
            vm.SettingsChanged += (_, updated) => applied = updated;

            vm.Settings.Audio.DeckADeviceId = 5;
            vm.NotifySettingsModified();
            vm.Apply();

            Assert.NotNull(applied);
            Assert.Equal(5, applied!.Audio.DeckADeviceId);
            Assert.False(vm.IsDirty);
        }

        [Fact]
        public void AddClockwheelSlotCommand_AppendsSlotAndSelectsIt()
        {
            var settings = new AppSettings();
            settings.Automation.Rotations = new System.Collections.ObjectModel.ObservableCollection<RotationDefinitionSettings> {
                new RotationDefinitionSettings { Name = "TestRotation" }
            };
            var vm = CreateViewModel(settings);
            vm.SelectedRotation = settings.Automation.Rotations[0];
            vm.AddClockwheelSlotCommand.Execute(null);

            Assert.Single(vm.SelectedRotation.Slots);
            Assert.Equal(vm.SelectedRotation.Slots[0], vm.SelectedClockwheelSlot);
        }

        [Fact]
        public void Cancel_RevertsPendingChanges()
        {
            var settings = new AppSettings();
            var vm = new SettingsViewModel(settings, autoDjSettingsService: new AutoDjSettingsService(loadFromDisk: false));

            vm.Settings.Audio.DeckADeviceId = 7;
            vm.NotifySettingsModified();
            vm.Cancel();

            Assert.Equal(settings.Audio.DeckADeviceId, vm.Settings.Audio.DeckADeviceId);
            Assert.False(vm.IsDirty);
        }

        [Fact]
        public void AddSimpleRotationCommand_AppendsRotationAndSelectsIt()
        {
            var settings = new AppSettings();
            settings.Automation.SimpleRotations = new ObservableCollection<Core.Automation.SimpleRotation>();
            var vm = CreateViewModel(settings);
            vm.AddSimpleRotationCommand.Execute(null);
            Assert.Single(vm.Settings.Automation.SimpleRotations);
            Assert.Same(vm.Settings.Automation.SimpleRotations[0], vm.SelectedSimpleRotation);
        }

        [Fact]
        public void RemoveSimpleRotationCommand_RemovesSelectedRotation()
        {
            var settings = new AppSettings();
            settings.Automation.SimpleRotations = new ObservableCollection<Core.Automation.SimpleRotation> {
                new Core.Automation.SimpleRotation { Name = "A1_unique" },
                new Core.Automation.SimpleRotation { Name = "B2_unique" }
            };
            var vm = CreateViewModel(settings);
            // Set selection to the actual instance in the collection
            var toRemove = vm.Settings.Automation.SimpleRotations.First(r => r.Name == "A1_unique");
            vm.SelectedSimpleRotation = toRemove;
            vm.RemoveSimpleRotationCommand.Execute(null);
            Assert.Contains(vm.Settings.Automation.SimpleRotations, r => r.Name == "B2_unique");
            Assert.DoesNotContain(vm.Settings.Automation.SimpleRotations, r => r.Name == "A1_unique");
        }

        [Fact]
        public void AddSimpleScheduleEntryCommand_AppendsEntryAndSelectsIt()
        {
            var settings = new AppSettings();
            settings.Automation.SimpleRotations = new ObservableCollection<Core.Automation.SimpleRotation> {
                new Core.Automation.SimpleRotation { Id = Guid.NewGuid(), Name = "Rot" }
            };
            settings.Automation.SimpleSchedule = new ObservableCollection<Core.Automation.SimpleSchedulerEntry>();
            var vm = CreateViewModel(settings);
            vm.AddSimpleScheduleEntryCommand.Execute(null);
            Assert.Single(vm.Settings.Automation.SimpleSchedule);
            Assert.Same(vm.Settings.Automation.SimpleSchedule[0], vm.SelectedSimpleScheduleEntry);
        }

        [Fact]
        public void RemoveSimpleScheduleEntryCommand_RemovesSelectedEntry()
        {
            var settings = new AppSettings();
            settings.Automation.SimpleSchedule = new ObservableCollection<Core.Automation.SimpleSchedulerEntry> {
                new Core.Automation.SimpleSchedulerEntry { RotationName = "A1_unique" },
                new Core.Automation.SimpleSchedulerEntry { RotationName = "B2_unique" }
            };
            var vm = CreateViewModel(settings);
            // Set selection to the actual instance in the collection
            var toRemove = vm.Settings.Automation.SimpleSchedule.First(e => e.RotationName == "A1_unique");
            vm.SelectedSimpleScheduleEntry = toRemove;
            vm.RemoveSimpleScheduleEntryCommand.Execute(null);
            Assert.Contains(vm.Settings.Automation.SimpleSchedule, e => e.RotationName == "B2_unique");
            Assert.DoesNotContain(vm.Settings.Automation.SimpleSchedule, e => e.RotationName == "A1_unique");
        }
    }
}
