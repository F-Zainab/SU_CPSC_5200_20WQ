using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using Newtonsoft.Json;
using restapi.Helpers;

namespace restapi.Models
{
    public class Timecard
    {
        public Timecard() { }

        public Timecard(int person)
        {
            Opened = DateTime.UtcNow;
            Employee = person;
            UniqueIdentifier = Guid.NewGuid();
            Lines = new List<TimecardLine>();
            Transitions = new List<Transition>();
        }

        public int Employee { get; set; }

        public TimecardStatus Status
        {
            get
            {
                return Transitions
                    .OrderByDescending(t => t.OccurredAt)
                    .First()
                    .TransitionedTo;
            }
        }

        [BsonIgnore]
        [JsonProperty("_self")]
        public string Self { get => $"/timesheets/{UniqueIdentifier}"; }

        public DateTime Opened { get; set; }

        [JsonIgnore]
        [BsonId]
        public ObjectId Id { get; set; }

        [JsonProperty("id")]
        public Guid UniqueIdentifier { get; set; }

        [JsonIgnore]
        public IList<TimecardLine> Lines { get; set; }

        [JsonIgnore]
        public IList<Transition> Transitions { get; set; }

        public IList<ActionLink> Actions { get => GetActionLinks(); }

        [JsonProperty("documentation")]
        public IList<DocumentLink> Documents { get => GetDocumentLinks(); }

        public string Version { get; set; } = "timecard-0.1";

        private IList<ActionLink> GetActionLinks()
        {
            var links = new List<ActionLink>();

            switch (Status)
            {
                case TimecardStatus.Draft:
                    links.Add(new ActionLink()
                    {
                        Method = Method.Post,
                        Type = ContentTypes.Cancellation,
                        Relationship = ActionRelationship.Cancel,
                        Reference = $"/timesheets/{UniqueIdentifier}/cancellation"
                    });

                    links.Add(new ActionLink()
                    {
                        Method = Method.Post,
                        Type = ContentTypes.Submittal,
                        Relationship = ActionRelationship.Submit,
                        Reference = $"/timesheets/{UniqueIdentifier}/submittal"
                    });

                    links.Add(new ActionLink()
                    {
                        Method = Method.Post,
                        Type = ContentTypes.TimesheetLine,
                        Relationship = ActionRelationship.RecordLine,
                        Reference = $"/timesheets/{UniqueIdentifier}/lines"
                    });
                    /////////////////Update (PATCH) a line item////////////////////////////////

                    links.Add(new ActionLink()
                    {
                        Method = Method.Patch,
                        Type = ContentTypes.TimesheetLine,
                        Relationship = ActionRelationship.RecordLine,
                        Reference = $"/timesheets/{UniqueIdentifier}/updateline/{UniqueIdentifier}"
                    });

                    links.Add(new ActionLink()
                    {
                        Method = Method.Post,
                        Type = ContentTypes.Timesheet,
                        Relationship = ActionRelationship.Create,
                        Reference = $"~/"
                    });
                    break;
                   
                case TimecardStatus.Submitted:
                    links.Add(new ActionLink()
                    {
                        Method = Method.Post,
                        Type = ContentTypes.Cancellation,
                        Relationship = ActionRelationship.Cancel,
                        Reference = $"/timesheets/{UniqueIdentifier}/cancellation"
                    });

                    links.Add(new ActionLink()
                    {
                        Method = Method.Post,
                        Type = ContentTypes.Rejection,
                        Relationship = ActionRelationship.Reject,
                        Reference = $"/timesheets/{UniqueIdentifier}/rejection"
                    });

                    links.Add(new ActionLink()
                    {
                        Method = Method.Post,
                        Type = ContentTypes.Approval,
                        Relationship = ActionRelationship.Approve,
                        Reference = $"/timesheets/{UniqueIdentifier}/approval"
                    });
                    break;

                case TimecardStatus.Approved:
                    // terminal state, nothing possible here
                    break;

                case TimecardStatus.Cancelled:
                    // terminal state, nothing possible here
                    ////Added Action link for the delete as the delete method is alredy present///////////////////////////
                    links.Add(new ActionLink()
                    {
                        Method = Method.Delete,
                        Relationship = ActionRelationship.Delete,
                        Reference = $"/timesheets/{UniqueIdentifier}"
                    });
                    break;
            }

            return links;
        }

        private IList<DocumentLink> GetDocumentLinks()
        {
            var links = new List<DocumentLink>();

            links.Add(new DocumentLink()
            {
                Method = Method.Get,
                Type = ContentTypes.Transitions,
                Relationship = DocumentRelationship.Transitions,
                Reference = $"/timesheets/{UniqueIdentifier}/transitions"
            });

            if (this.Lines.Count > 0)
            {
                links.Add(new DocumentLink()
                {
                    Method = Method.Get,
                    Type = ContentTypes.TimesheetLine,
                    Relationship = DocumentRelationship.Lines,
                    Reference = $"/timesheets/{UniqueIdentifier}/lines"
                });
            }

            if (this.Status == TimecardStatus.Submitted)
            {
                links.Add(new DocumentLink()
                {
                    Method = Method.Get,
                    Type = ContentTypes.Transitions,
                    Relationship = DocumentRelationship.Submittal,
                    Reference = $"/timesheets/{UniqueIdentifier}/submittal"
                });
            }

            return links;
        }

        public TimecardLine AddLine(DocumentLine documentLine)
        {
            var annotatedLine = new TimecardLine(documentLine);

            Lines.Add(annotatedLine);

            return annotatedLine;
        }

        /*************************Replace (POST) a complete line item*******************************************/
        public TimecardLine ReplaceLine(Guid lid, DocumentLine documentline)
        {
            var line = Lines.FirstOrDefault(l => l.UniqueIdentifier == lid);
            var annotatedLine = new TimecardLine(documentline);
            annotatedLine.UniqueIdentifier = lid;
            Lines.Remove(line);
            Lines.Add(annotatedLine);

            return annotatedLine;
        }

        /******************Update (PATCH) a line item****************************************/
        public TimecardLine UpdateLineItem(Guid lid, DocumentLine item)
        {
            var line = Lines.FirstOrDefault(l => l.UniqueIdentifier == lid);
            line.Update(item);

            return line;
        }

        public bool CanBeDeleted()
        {
            return (Status == TimecardStatus.Cancelled || Status == TimecardStatus.Draft);
        }

        public bool HasLine(Guid lineId)
        {
            return Lines
                .Any(l => l.UniqueIdentifier == lineId);
        }


        public override string ToString()
        {
            return PublicJsonSerializer.SerializeObjectIndented(this);
        }
    }
}
