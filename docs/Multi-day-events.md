# Multi-day events and official FFXIV dates

In **Create Event → Schedule**, choose:

- **Single session**: a start and duration, as before.
- **Continuous multi-day**: explicit start and end dates/times. Every overlapping calendar day shows the event.
- **Multiple sessions**: up to 12 separate start/end pairs within 366 days. Times can differ each day. Gaps stay empty on the in-game calendar. Sessions cannot overlap; ambiguous or skipped daylight-saving times must be changed.

Calendar capsules use consistent lanes across each week. Month view shows up to four lanes and week view up to seven; +more indicates additional events in the full list. Select any active day to see the complete event list below it. Player events are listed before official entries. The event's View lists every session in your local time zone.

Your personal on-screen reminder follows each session. Dismissing one occurrence does not dismiss the next. Opted-in personal Discord DMs also follow each session, even out of game; missed reminder times are skipped.

## Discord and Google

The group remains one Event Horizon event, one Discord scheduled event/announcement, and one managed Google entry per subscriber. Discord and Google use the earliest start and latest end, with the complete session itinerary in the description/card. Google marks multiple-session entries **Free** so the gaps do not block availability. Google’s own reminder applies to the overall event start, not each session.

Discord's native 1,000-character description reserves room for the itinerary. If necessary, it shortens the description and points to the announcement, which retains the full text. Choose an announcement channel when publishing long descriptions with multiple sessions. The existing requirement to publish/sync community events before their first start still applies.

Deploy the updated Cloudflare relay before using these schedules with Discord or Google; an older relay does not understand the additional dates. Existing single-session events remain compatible.

## Official game events

**Events → Show official FFXIV events** is enabled by default and saves locally. Official entries are separate from player records and are read-only. They cannot be edited, duplicated, published, subscribed to Google Calendar, or configured for reminders. **View → Open Square Enix announcement** opens the source.

The catalogue is bundled with plugin updates, not scraped automatically. Verified September 28, 2026. Dates below are the published Pacific times; the plugin stores UTC and displays your local time:

| Event | Starts (PDT) | Ends (PDT) | Source |
|---|---|---|---|
| Yo-kai Watch: Gather One, Gather All | Aug 4, 2026, 1:00 AM | Oct 5, 2026, 7:59 AM | [Square Enix announcement](https://na.finalfantasyxiv.com/lodestone/topics/detail/5e9868fb47977cbd45effe2a50bdb78a2270430c), [publisher's dated Steam announcement](https://steamcommunity.com/app/39210/allnews/) |
| Moogle Treasure Trove — The First Hunt for Astronomy | Sep 9, 2026, 1:00 AM | Oct 19, 2026, 7:59 AM | [Official event site](https://na.finalfantasyxiv.com/lodestone/special/mogmog-collection/202609/lsekubsk45) |
| A Nocturne for Heroes | Sep 24, 2026, 1:00 AM | Oct 13, 2026, 7:59 AM | [Square Enix announcement](https://na.finalfantasyxiv.com/lodestone/topics/detail/b7ff81b627294a36825255532371d6d8f9066d74) |

No confirmed later seasonal dates or 2027 dates were found during this check. They are omitted rather than estimated. Square Enix can change event times; use the source link for the latest details. These entries apply to the global game service described on the linked Lodestone pages.

See [Calendar event types](Calendar-event-types.md) for the capsule legend, hover, selection and tags.
