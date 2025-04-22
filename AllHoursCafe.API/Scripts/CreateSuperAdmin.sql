-- Script to create a SuperAdmin user
-- Password is hashed using BCrypt, default password is 'SuperAdmin123!'
-- The hash below corresponds to 'SuperAdmin123!'

-- Check if the SuperAdmin already exists
IF NOT EXISTS (SELECT * FROM Users WHERE Email = 'superadmin@allhourscafe.com')
BEGIN
    -- Insert the SuperAdmin user
    INSERT INTO Users (
        FullName,
        Email,
        PasswordHash,
        IsActive,
        CreatedAt,
        Role
    )
    VALUES (
        'Super Administrator',
        'superadmin@allhourscafe.com',
        '$2a$11$Uj7.BR5vGBnRpIxNEPDK8.XJOm.1UPmhV7qFoYfKn1hht/6BUkHsO', -- BCrypt hash for 'SuperAdmin123!'
        1, -- IsActive = true
        GETDATE(), -- Current date/time
        'SuperAdmin'
    );
    
    PRINT 'SuperAdmin user created successfully.';
END
ELSE
BEGIN
    PRINT 'SuperAdmin user already exists.';
END
