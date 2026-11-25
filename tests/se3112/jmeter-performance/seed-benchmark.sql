\set ON_ERROR_STOP on
BEGIN;
LOCK TABLE public."Events", public."TicketTypes" IN SHARE ROW EXCLUSIVE MODE;

CREATE TEMP TABLE benchmark_fixture ON COMMIT DROP AS
SELECT n,
       ('5e311200-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid AS event_id,
       ('5e311201-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid AS ticket_id,
       'SE3112 Benchmark Event ' || lpad(n::text, 3, '0') AS title,
       timestamptz '2028-01-01 12:00:00+00' + (n - 1) * interval '1 day' AS event_date,
       (1000 + (n % 10) * 100)::numeric(18,2) AS price
FROM generate_series(1, 100) AS series(n);

DO $$
BEGIN
    IF current_database() <> 'eventpulse_events'
        OR EXISTS (SELECT FROM benchmark_fixture WHERE event_date <= CURRENT_TIMESTAMP) THEN
        RAISE EXCEPTION 'Unexpected database or expired benchmark dates';
    END IF;
    IF EXISTS (
        SELECT FROM public."Events" e
        WHERE e."Status" = 'Published' AND e."EventDate" > CURRENT_TIMESTAMP
          AND NOT EXISTS (SELECT FROM benchmark_fixture f WHERE f.event_id = e."Id")
    ) THEN
        RAISE EXCEPTION 'Unrelated visible events would contaminate the benchmark';
    END IF;
END $$;

INSERT INTO public."Events" (
    "Id", "Title", "Description", "Venue", "EventDate", "Price", "Status",
    "OrganizerId", "CreatedAt", "ReviewedAt", "ReviewedBy", "Category", "VenueType"
)
SELECT event_id, title,
       'Synthetic event for the EventPulse performance assessment. Program includes a welcome session, scheduled presentations and a moderated discussion.',
       'Benchmark Hall ' || (1 + n % 5),
       event_date, price, 'Approved',
       '5e311202-0000-4000-8000-000000000001',
       timestamptz '2026-01-01 00:00:00+00',
       timestamptz '2026-01-02 00:00:00+00',
       '5e311202-0000-4000-8000-000000000002',
       CASE WHEN n % 2 = 0 THEN 'Conference' ELSE 'Workshop' END,
       'Indoor'
FROM benchmark_fixture
ON CONFLICT ("Id") DO NOTHING;

DO $$
BEGIN
    IF EXISTS (
        SELECT FROM benchmark_fixture f
        JOIN public."Events" e ON e."Id" = f.event_id
        WHERE (e."Title", e."EventDate", e."Price", e."OrganizerId")
            IS DISTINCT FROM (f.title, f.event_date, f.price, '5e311202-0000-4000-8000-000000000001'::uuid)
           OR e."Status" NOT IN ('Approved', 'Published')
    ) THEN
        RAISE EXCEPTION 'Unexpected event at a reserved fixture ID';
    END IF;
END $$;

INSERT INTO public."TicketTypes" ("Id", "EventId", "Name", "Price", "Capacity", "BookedQuantity", "CreatedAt")
SELECT ticket_id, event_id, 'General Admission', price, 100, 0, timestamptz '2026-01-02 00:00:00+00'
FROM benchmark_fixture
ON CONFLICT ("Id") DO NOTHING;

DO $$
BEGIN
    IF EXISTS (
        SELECT FROM benchmark_fixture f
        JOIN public."TicketTypes" t ON t."Id" = f.ticket_id
        WHERE (t."EventId", t."Name", t."Price", t."Capacity", t."BookedQuantity")
            IS DISTINCT FROM (f.event_id, 'General Admission', f.price, 100, 0)
    ) THEN
        RAISE EXCEPTION 'Unexpected ticket at a reserved fixture ID';
    END IF;
END $$;

UPDATE public."Events" e SET "Status" = 'Published'
FROM benchmark_fixture f
JOIN public."TicketTypes" t ON t."Id" = f.ticket_id AND t."EventId" = f.event_id
WHERE e."Id" = f.event_id AND e."Status" = 'Approved';

SELECT count(*) AS published_fixture_events
FROM public."Events" e JOIN benchmark_fixture f ON e."Id" = f.event_id
WHERE e."Status" = 'Published' AND e."EventDate" > CURRENT_TIMESTAMP;
COMMIT;
