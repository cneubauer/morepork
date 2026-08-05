INSERT INTO tenant (id, name) VALUES (1, 'demo');

INSERT INTO stack_instance (id, state_tenant, state_zone) VALUES (1, 1, 1);

INSERT INTO system_instance (id, stack_instance_id) VALUES (5000000001, 1);

INSERT INTO desired_state (
    stack_instance_id, system_instance_id, state_namespace, state_zone, state_version,
    tenant, tombstoned, data, created
)
VALUES (
    1, 5000000001, 3, 1, 0,
    1, false, '{"webspace": {}}'::jsonb, (NOW() AT TIME ZONE 'utc')
);
