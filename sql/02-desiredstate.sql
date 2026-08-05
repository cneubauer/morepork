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
    PRIMARY KEY (stack_instance_id, system_instance_id, state_namespace, state_zone, state_version)
);
CREATE INDEX ON desired_state (next_check);
CREATE INDEX ON desired_state (tenant, state_namespace, state_zone, stack_instance_id, state_version DESC) WHERE expired IS NULL;

CREATE TABLE lookup_resource (
    stack_instance_id BIGINT NOT NULL,
    system_instance_id BIGINT NOT NULL DEFAULT 0,
    state_namespace SMALLINT NOT NULL,
    state_zone SMALLINT NOT NULL,
    tenant SMALLINT NOT NULL,
    resource_key SMALLINT NOT NULL,
    resource_text VARCHAR(255) NOT NULL,
    resource_text_reverse VARCHAR(255) NOT NULL,
    PRIMARY KEY (stack_instance_id, system_instance_id, state_namespace, state_zone, tenant, resource_key, resource_text)
);
CREATE INDEX ON lookup_resource (resource_key);
CREATE INDEX ON lookup_resource (resource_text);
