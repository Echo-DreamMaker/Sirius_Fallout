cmd-atvrange-desc = Устанавливает диапазон отладки атмосферы (два числа с плавающей точкой: начало [красный] и конец [синий])
cmd-atvrange-help = Использование: { $command } <start> <end>
cmd-atvrange-error-start = Некорректное число START
cmd-atvrange-error-end = Некорректное число END
cmd-atvrange-error-zero = Масштаб не может быть равен нулю, так как это вызовет деление на ноль в AtmosDebugOverlay.

cmd-atvmode-desc = Устанавливает режим отладки атмосферы. Масштаб будет автоматически сброшен.
cmd-atvmode-help = Использование: { $command } <TotalMoles/GasMoles/Temperature> [<ID газа (для GasMoles)>]
cmd-atvmode-error-invalid = Недопустимый режим
cmd-atvmode-error-target-gas = Для этого режима необходимо указать целевой газ.
cmd-atvmode-error-out-of-range = ID газа не удалось разобрать или он вне диапазона.
cmd-atvmode-error-info = Для этого режима дополнительная информация не требуется.

cmd-atvcbm-desc = Переключает отображение с красного/зелёного/синего на оттенки серого
cmd-atvcbm-help = Использование: { $command } <true/false>
cmd-atvcbm-error = Недопустимый флаг
