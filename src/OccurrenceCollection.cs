﻿using Microsoft.Exchange.WebServices.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.Exchange.WebServices.Data.Recurrence;
using Task = System.Threading.Tasks.Task;

namespace Exchange.WebServices.OccurrenceData
{
    public class OccurrenceCollection : List<Occurrence>
    {
        internal OccurrenceCollection()
        {
        }

        /// <summary>
        /// Binds to occurrences of an existing appointment. Calling this method results in a call to Exchange Web Services (EWS).
        /// </summary>
        /// <param name="service">The service.</param>
        /// <param name="recurringMasterId">The recurring master identifier.</param>
        /// <returns>Appointment occurrences that corresponds to the specified master identifier.</returns>
        public static async Task<OccurrenceCollection> Bind(ExchangeService service, ItemId recurringMasterId)
        {
            var definition = new PropertyDefinition[]
            {
                AppointmentSchema.Recurrence,
                AppointmentSchema.ModifiedOccurrences,
                AppointmentSchema.DeletedOccurrences
            };

            var propertySet = new PropertySet(BasePropertySet.FirstClassProperties, definition) { RequestedBodyType = BodyType.Text };

            var appointment = await Appointment.Bind(service, recurringMasterId, propertySet);

            if (!appointment.Recurrence.HasEnd)
            {
                return new OccurrenceCollection();
            }

            var defaultOccurrence = Mapping(appointment);

            var result = new OccurrenceCollection();

            if (appointment.Recurrence is DailyPattern dailyPattern)
            {
                result = PatternConverter.Convert(dailyPattern, defaultOccurrence);
            }
            else if (appointment.Recurrence is WeeklyPattern weeklyPattern)
            {
                result = PatternConverter.Convert(weeklyPattern, defaultOccurrence);
            }
            else if (appointment.Recurrence is MonthlyPattern monthlyPattern)
            {
                result = PatternConverter.Convert(monthlyPattern, defaultOccurrence);
            }
            else if (appointment.Recurrence is YearlyPattern yearlyPattern)
            {
                result = PatternConverter.Convert(yearlyPattern, defaultOccurrence);
            }
            else if (appointment.Recurrence is RelativeMonthlyPattern relativeMonthlyPattern)
            {
                result = PatternConverter.Convert(relativeMonthlyPattern, defaultOccurrence);
            }
            else if (appointment.Recurrence is RelativeYearlyPattern relativeYearlyPattern)
            {
                result = PatternConverter.Convert(relativeYearlyPattern, defaultOccurrence);
            }

            result.CancelledAll(appointment.DeletedOccurrences);

            await result.UpdateAllAsync(service, appointment.ModifiedOccurrences);

            return result;
        }

        private void CancelledAll(DeletedOccurrenceInfoCollection deletedOccurrences)
        {
            if (deletedOccurrences == null)
            {
                return;
            }

            FindAll(m => deletedOccurrences.Any(d => d.OriginalStart == m.Start)).ForEach(m => m.IsCancelled = true);
        }

        private async Task UpdateAllAsync(ExchangeService service, OccurrenceInfoCollection modifiedOccurrences)
        {
            if (modifiedOccurrences == null)
            {
                return;
            }

            foreach (var item in modifiedOccurrences)
            {
                var occurrence = Find(m => m.Start == item.OriginalStart);
                var appointment = await Appointment.Bind(service, item.ItemId, new PropertySet(BasePropertySet.FirstClassProperties) { RequestedBodyType = BodyType.Text });

                if (occurrence == null || appointment == null)
                {
                    break;
                }

                occurrence.Start = appointment.Start;
                occurrence.End = appointment.End;
                occurrence.IsAllDayEvent = appointment.IsAllDayEvent;
                occurrence.IsCancelled = appointment.IsCancelled;
                occurrence.LastModifiedTime = appointment.LastModifiedTime;
                occurrence.Sensitivity = appointment.Sensitivity;
                occurrence.Subject = appointment.Subject;
                occurrence.Text = appointment.Body;
            }
        }

        private static Occurrence Mapping(Appointment appointment)
        {
            if (appointment == null)
            {
                return null;
            }

            return new Occurrence
            {
                Start = appointment.Start,
                End = appointment.End,
                IsAllDayEvent = appointment.IsAllDayEvent,
                IsCancelled = appointment.IsCancelled,
                LastModifiedTime = appointment.LastModifiedTime,
                MasterAppointmentId = appointment.Id.UniqueId,
                Sensitivity = appointment.Sensitivity,
                Subject = appointment.Subject,
                Text = appointment.Body
            };
        }
    }
}
