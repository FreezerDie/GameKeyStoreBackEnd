-- Clear existing permissions for role 1 (admin)
DELETE FROM role_permissions WHERE role_id = 1;

-- Insert all admin permissions
INSERT INTO role_permissions (role_id, premission, granted_at) VALUES 
(1, 'games.read', NOW()),
(1, 'games.create', NOW()),
(1, 'games.update', NOW()),
(1, 'games.delete', NOW()),
(1, 'games.admin', NOW()),
(1, 'users.read', NOW()),
(1, 'users.create', NOW()),
(1, 'users.update', NOW()),
(1, 'gamekeys.read', NOW()),
(1, 'gamekeys.create', NOW()),
(1, 'gamekeys.update', NOW()),
(1, 'gamekeys.delete', NOW()),
(1, 'gamekeys.admin', NOW()),
(1, 'categories.read', NOW()),
(1, 'categories.create', NOW()),
(1, 'categories.update', NOW()),
(1, 'categories.delete', NOW()),
(1, 'categories.admin', NOW()),
(1, 'orders.read', NOW()),
(1, 'orders.create', NOW()),
(1, 'orders.update', NOW()),
(1, 'orders.delete', NOW()),
(1, 'orders.admin', NOW()),
(1, 'reports.read', NOW()),
(1, 'reports.admin', NOW()),
(1, 'roles.read', NOW()),
(1, 'roles.create', NOW()),
(1, 'roles.update', NOW()),
(1, 'roles.delete', NOW()),
(1, 'roles.admin', NOW()),
(1, 'permissions.read', NOW()),
(1, 'permissions.manage', NOW());

-- Verify the insertions
SELECT COUNT(*) as inserted_permissions_count FROM role_permissions WHERE role_id = 1;

-- Show all permissions for role 1
SELECT role_id, premission, granted_at FROM role_permissions WHERE role_id = 1 ORDER BY premission;
