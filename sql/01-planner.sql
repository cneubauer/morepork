CREATE TABLE tenant (
    id SMALLINT NOT NULL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE
);

CREATE TABLE stack_instance (
    id BIGSERIAL NOT NULL PRIMARY KEY,
    state_tenant SMALLINT NOT NULL REFERENCES tenant (id),
    tombstoned BOOLEAN NOT NULL DEFAULT FALSE,
    state_zone SMALLINT NOT NULL,
    dependency_mode BIGINT NOT NULL DEFAULT 0,
    created TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    ext_reference VARCHAR(255),
    tags VARCHAR(20)[10],
    expired TIMESTAMP
);

CREATE TABLE system_instance (
    id BIGSERIAL NOT NULL PRIMARY KEY,
    stack_instance_id BIGINT NOT NULL REFERENCES stack_instance (id) ON DELETE CASCADE,
    created TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);
ALTER SEQUENCE system_instance_id_seq RESTART WITH 5000000000;
