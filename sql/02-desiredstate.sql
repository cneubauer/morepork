CREATE TABLE desired_state (
    stack_instance_id BIGINT NOT NULL,
    system_instance_id BIGINT NOT NULL DEFAULT 0,
    state_namespace SMALLINT NOT NULL,
    state_zone SMALLINT NOT NULL,
    state_version BIGINT NOT NULL,
    tenant SMALLINT NOT NULL DEFAULT 1,
    tombstoned BOOLEAN NOT NULL,
    data jsonb NOT NULL,
    created TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    applied TIMESTAMP WITH TIME ZONE,
    expired TIMESTAMP WITH TIME ZONE,
    next_check TIMESTAMP WITH TIME ZONE,
    transaction_id VARCHAR(255) NOT NULL,
    PRIMARY KEY (stack_instance_id, system_instance_id, state_namespace, state_zone, state_version)
);

ALTER TABLE desired_state ADD CONSTRAINT desired_state_transaction_id_key UNIQUE (tenant, state_namespace, transaction_id);

CREATE INDEX ON desired_state (next_check);
CREATE INDEX ON desired_state (tenant, state_namespace, state_zone, stack_instance_id, state_version DESC) WHERE expired IS NULL;

CREATE TABLE outbox (
    context jsonb NOT NULL,
    created TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    -- The scheduling request owns this entry until the lease expires. Recovery only claims
    -- entries whose owner never started the workflow, i.e. crashed before the lease ran out.
    leased_until TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW() + INTERVAL '1 minute'
);
CREATE INDEX ON outbox (created);
CREATE INDEX ON outbox (leased_until);


CREATE TABLE notifications (
    notification_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    notification_type SMALLINT NOT NULL,
    -- A snapshot of the desired state the notification is about, so notifications can be
    -- filtered and returned without joining desired_state.
    tenant SMALLINT NOT NULL REFERENCES tenant (id),
    stack_instance_id BIGINT NOT NULL,
    system_instance_id BIGINT NOT NULL,
    state_namespace SMALLINT NOT NULL,
    state_zone SMALLINT NOT NULL,
    state_version BIGINT NOT NULL,
    tombstoned BOOLEAN NOT NULL,
    payload jsonb,
    transaction_id VARCHAR(255) NOT NULL,
    created TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX ON notifications (tenant, notification_id);
CREATE INDEX ON notifications (transaction_id);

CREATE TABLE notification_pointer (
    username VARCHAR(255) PRIMARY KEY,
    pointer BIGINT NOT NULL DEFAULT 0,
    last_modified TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
