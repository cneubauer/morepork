CREATE TABLE outbox (
    workflow_id VARCHAR(255) NOT NULL PRIMARY KEY,
    stack_instance_id BIGINT NOT NULL,
    system_instance_id BIGINT NOT NULL DEFAULT 0,
    created TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    -- The scheduling request owns this entry until the lease expires. Recovery only claims
    -- entries whose owner never started the workflow, i.e. crashed before the lease ran out.
    leased_until TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);
CREATE INDEX ON outbox (created);
CREATE INDEX ON outbox (leased_until);
