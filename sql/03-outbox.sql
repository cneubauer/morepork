CREATE TABLE outbox (
    transaction_id VARCHAR(255) NOT NULL PRIMARY KEY,
    stack_instance_id BIGINT NOT NULL,
    system_instance_id BIGINT NOT NULL DEFAULT 0,
    created TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);
CREATE INDEX ON outbox (created);
