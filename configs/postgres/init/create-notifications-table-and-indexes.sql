-- Switch to the NotificationsMicroservice logical database created by create-notifications-database.sql.
\c notificationsdatabase;

CREATE TABLE IF NOT EXISTS public."Notifications"
(
    "NotificationId" uuid NOT NULL,
    "SequenceNumber" bigint GENERATED ALWAYS AS IDENTITY,
    "PayloadHash" character varying(64) NOT NULL,
    "UserId" character varying(200) NOT NULL,
    "UserEmail" character varying(320) NOT NULL,
    "Culture" character varying(20) NOT NULL,
    "Operation" character varying(16) NOT NULL,
    "Status" character varying(16) NOT NULL,
    "ProductId" uuid NULL,
    "ProductName" character varying(200) NULL,
    "ProductVersion" integer NULL,
    "ErrorCode" character varying(100) NULL,
    "TraceParent" character varying(512) NULL,
    "TraceState" character varying(512) NULL,
    "CorrelationId" character varying(200) NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    "DeliveryStatus" character varying(32) NOT NULL DEFAULT 'Pending',
    "AttemptCount" integer NOT NULL DEFAULT 0,
    "NextAttemptAtUtc" timestamp with time zone NULL,
    "AckDeadlineUtc" timestamp with time zone NULL,
    "LastError" character varying(100) NULL,
    "LockedBy" character varying(300) NULL,
    "LockedUntilUtc" timestamp with time zone NULL,
    "Version" bigint NOT NULL DEFAULT 0,
    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "DeliveredAtUtc" timestamp with time zone NULL,
    "ReadAtUtc" timestamp with time zone NULL,
    "EmailProviderMessageId" character varying(200) NULL,
    CONSTRAINT "Notifications_pkey" PRIMARY KEY ("NotificationId"),
    CONSTRAINT "Notifications_SequenceNumber_key" UNIQUE ("SequenceNumber"),
    CONSTRAINT "Notifications_AttemptCount_check" CHECK ("AttemptCount" >= 0),
    CONSTRAINT "Notifications_DeliveryStatus_check" CHECK
    (
        "DeliveryStatus" IN
        ('Pending', 'AwaitingSseAck', 'RetryScheduled', 'SendingEmail',
         'DeliveredInApp', 'DeliveredEmail', 'Failed')
    )
);

CREATE INDEX IF NOT EXISTS "IX_Notifications_User_Sequence"
    ON public."Notifications" ("UserId", "SequenceNumber" DESC);

CREATE INDEX IF NOT EXISTS "IX_Notifications_DeliveryScan"
    ON public."Notifications"
    ("DeliveryStatus", "NextAttemptAtUtc", "AckDeadlineUtc", "LockedUntilUtc", "CreatedAtUtc");

CREATE INDEX IF NOT EXISTS "IX_Notifications_Unread"
    ON public."Notifications" ("UserId", "SequenceNumber" DESC)
    WHERE "ReadAtUtc" IS NULL;

ALTER TABLE IF EXISTS public."Notifications"
    ADD COLUMN IF NOT EXISTS "TraceParent" character varying(512),
    ADD COLUMN IF NOT EXISTS "TraceState" character varying(512);
