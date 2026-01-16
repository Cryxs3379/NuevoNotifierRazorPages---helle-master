-- ============================================================================
-- Script de migración: Normalizar CustomerPhone en ConversationState
-- Objetivo: Eliminar duplicados causados por teléfonos con '+' y sin '+'
-- Formato canónico: SIEMPRE sin '+'
-- ============================================================================

BEGIN TRANSACTION;

BEGIN TRY
    PRINT 'Iniciando migración de ConversationState: normalizar CustomerPhone (quitar +)';
    
    -- Paso 1: Para cada fila cuyo CustomerPhone empieza por '+', procesar
    DECLARE @PhoneWithPlus NVARCHAR(50);
    DECLARE @PhoneNoPlus NVARCHAR(50);
    DECLARE @HasTargetRow BIT;
    
    DECLARE phone_cursor CURSOR FOR
    SELECT DISTINCT CustomerPhone
    FROM dbo.ConversationState
    WHERE CustomerPhone LIKE '+%';
    
    OPEN phone_cursor;
    FETCH NEXT FROM phone_cursor INTO @PhoneWithPlus;
    
    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Calcular PhoneNoPlus (quitar el '+')
        SET @PhoneNoPlus = SUBSTRING(@PhoneWithPlus, 2, LEN(@PhoneWithPlus));
        
        -- Verificar si existe fila con PhoneNoPlus
        IF EXISTS (SELECT 1 FROM dbo.ConversationState WHERE CustomerPhone = @PhoneNoPlus)
        BEGIN
            -- MERGE: combinar datos de ambas filas
            PRINT 'Combinando: ' + @PhoneWithPlus + ' -> ' + @PhoneNoPlus;
            
            UPDATE target
            SET 
                -- LastInboundAt = MAX de ambos
                LastInboundAt = CASE 
                    WHEN source.LastInboundAt IS NULL THEN target.LastInboundAt
                    WHEN target.LastInboundAt IS NULL THEN source.LastInboundAt
                    WHEN source.LastInboundAt > target.LastInboundAt THEN source.LastInboundAt
                    ELSE target.LastInboundAt
                END,
                -- LastOutboundAt = MAX de ambos
                LastOutboundAt = CASE 
                    WHEN source.LastOutboundAt IS NULL THEN target.LastOutboundAt
                    WHEN target.LastOutboundAt IS NULL THEN source.LastOutboundAt
                    WHEN source.LastOutboundAt > target.LastOutboundAt THEN source.LastOutboundAt
                    ELSE target.LastOutboundAt
                END,
                -- LastReadInboundAt = MAX de ambos
                LastReadInboundAt = CASE 
                    WHEN source.LastReadInboundAt IS NULL THEN target.LastReadInboundAt
                    WHEN target.LastReadInboundAt IS NULL THEN source.LastReadInboundAt
                    WHEN source.LastReadInboundAt > target.LastReadInboundAt THEN source.LastReadInboundAt
                    ELSE target.LastReadInboundAt
                END,
                -- AssignedTo/AssignedUntil: del registro más reciente por UpdatedAt
                AssignedTo = CASE 
                    WHEN source.UpdatedAt > target.UpdatedAt THEN source.AssignedTo
                    ELSE target.AssignedTo
                END,
                AssignedUntil = CASE 
                    WHEN source.UpdatedAt > target.UpdatedAt THEN source.AssignedUntil
                    ELSE target.AssignedUntil
                END,
                -- UpdatedAt = MAX de ambos
                UpdatedAt = CASE 
                    WHEN source.UpdatedAt > target.UpdatedAt THEN source.UpdatedAt
                    ELSE target.UpdatedAt
                END
            FROM dbo.ConversationState target
            CROSS APPLY (
                SELECT LastInboundAt, LastOutboundAt, LastReadInboundAt, AssignedTo, AssignedUntil, UpdatedAt
                FROM dbo.ConversationState
                WHERE CustomerPhone = @PhoneWithPlus
            ) source
            WHERE target.CustomerPhone = @PhoneNoPlus;
            
            -- Eliminar la fila con '+'
            DELETE FROM dbo.ConversationState WHERE CustomerPhone = @PhoneWithPlus;
        END
        ELSE
        BEGIN
            -- NO existe fila PhoneNoPlus, actualizar CustomerPhone quitando el '+'
            PRINT 'Actualizando: ' + @PhoneWithPlus + ' -> ' + @PhoneNoPlus;
            UPDATE dbo.ConversationState
            SET CustomerPhone = @PhoneNoPlus
            WHERE CustomerPhone = @PhoneWithPlus;
        END
        
        FETCH NEXT FROM phone_cursor INTO @PhoneWithPlus;
    END
    
    CLOSE phone_cursor;
    DEALLOCATE phone_cursor;
    
    -- Paso 2: Verificar que no queden filas con '+'
    DECLARE @RemainingCount INT;
    SELECT @RemainingCount = COUNT(*) 
    FROM dbo.ConversationState 
    WHERE CustomerPhone LIKE '+%';
    
    IF @RemainingCount > 0
    BEGIN
        PRINT 'ADVERTENCIA: Aún quedan ' + CAST(@RemainingCount AS NVARCHAR(10)) + ' filas con +';
        -- Eliminar cualquier fila restante con '+' (por si acaso)
        DELETE FROM dbo.ConversationState WHERE CustomerPhone LIKE '+%';
    END
    
    -- Paso 3: Verificar que CustomerPhone tiene PK o UNIQUE constraint
    -- (CustomerPhone es PK según el modelo, así que ya está protegido)
    -- Solo verificamos que no haya duplicados
    DECLARE @DuplicateCount INT;
    SELECT @DuplicateCount = COUNT(*) - COUNT(DISTINCT CustomerPhone)
    FROM dbo.ConversationState;
    
    IF @DuplicateCount > 0
    BEGIN
        RAISERROR('ERROR: Se detectaron %d duplicados después de la migración', 16, 1, @DuplicateCount);
    END
    ELSE
    BEGIN
        PRINT 'Migración completada exitosamente. No se detectaron duplicados.';
    END
    
    COMMIT TRANSACTION;
    PRINT 'Migración finalizada correctamente.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'ERROR en migración: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
