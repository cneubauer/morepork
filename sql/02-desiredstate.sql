CREATE TABLE desired_state (
    stack_instance_id BIGINT NOT NULL,
    system_instance_id BIGINT NOT NULL DEFAULT 0,
    state_namespace SMALLINT NOT NULL,
    state_zone SMALLINT NOT NULL,
    state_version BIGINT NOT NULL,
    tenant SMALLINT NOT NULL DEFAULT 1,
    tombstoned BOOLEAN NOT NULL,
    data jsonb NOT NULL,
    created TIMESTAMP NOT NULL,
    applied TIMESTAMP,
    expired TIMESTAMP,
    next_check TIMESTAMP WITH TIME ZONE,
    transaction_id VARCHAR(255) NOT NULL PRIMARY KEY,
    PRIMARY KEY (stack_instance_id, system_instance_id, state_namespace, state_zone, state_version)
);
CREATE INDEX ON desired_state (next_check);
CREATE INDEX ON desired_state (tenant, state_namespace, state_zone, stack_instance_id, state_version DESC) WHERE expired IS NULL;

CREATE TABLE outbox (
    transaction_id VARCHAR(255) NOT NULL PRIMARY KEY,
    stack_instance_id BIGINT NOT NULL,
    system_instance_id BIGINT NOT NULL DEFAULT 0,
    created TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    -- The scheduling request owns this entry until the lease expires. Recovery only claims
    -- entries whose owner never started the workflow, i.e. crashed before the lease ran out.
    leased_until TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);
CREATE INDEX ON outbox (created);
CREATE INDEX ON outbox (leased_until);
