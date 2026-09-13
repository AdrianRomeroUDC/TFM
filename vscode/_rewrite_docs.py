import ast
import io
import os
import tokenize


TEMPLATE_MARKERS = (
    'Realiza la operacion',
    'Obtiene o transforma los datos gestionados',
    'Valor de entrada utilizado por la operacion',
    'No recibe parametros',
    'El resultado de la operacion, cuando existe; en otro caso, None.',
    'El valor calculado o recuperado por la operacion.',
)

ARGUMENTS = {
    'axis': 'Número lógico del eje que se consulta o controla.',
    'direction': 'Sentido del movimiento del eje.',
    'ref_mode': 'Indica si el movimiento corresponde a una búsqueda de referencia.',
    'num': 'Número lógico del eje o elemento que se procesa.',
    'rv': 'Desplazamiento o sentido relativo del movimiento.',
    'av': 'Posición absoluta de destino.',
    'value': 'Valor que se asigna al parámetro o estado compartido.',
    'state': 'Código o valor del estado que se actualiza.',
    '_code': 'Código de estado que se publica o registra.',
    '_active': 'Indicador que señala si el estado está activo.',
    'message': 'Mensaje recibido desde el transporte MQTT.',
    'event': 'Evento generado por el sensor o dispositivo asociado.',
    'topic': 'Tema MQTT al que se publica el mensaje.',
    'data': 'Contenido que se publica o procesa.',
    'qos': 'Nivel de calidad de servicio MQTT.',
    'retain': 'Indica si el broker debe conservar el último mensaje.',
    'name': 'Nombre simbólico de la posición o configuración.',
    'idx': 'Índice de la posición dentro de la estructura de calibración.',
    '_data': 'Estructura de datos de calibración que se almacena.',
    'wp': 'Pieza o posición de trabajo solicitada.',
    'cmd': 'Comando de movimiento que se ejecuta.',
    'degree': 'Ángulo objetivo del movimiento.',
    'speed': 'Velocidad configurada para el mecanismo.',
    'myList': 'Secuencia numérica cuyos valores se agregan.',
    'payload': 'Contenido de la carga útil que se envía o transforma.',
    'payload_text': 'Representación textual de la carga MQTT.',
    'measurement': 'Nombre de la medición de InfluxDB.',
    'tags': 'Etiquetas asociadas a la medición.',
    'fields': 'Campos y valores de la medición.',
    'ts_ms': 'Marca temporal en milisegundos.',
    'ts_value': 'Marca temporal recibida, en segundos, milisegundos o formato ISO.',
    'lines': 'Líneas de protocolo pendientes de enviar.',
    '_tr0': 'Nivel numérico para el trazado detallado.',
    '_tr': 'Nivel numérico para el trazado normal.',
    '_dg': 'Nivel numérico para los mensajes de depuración.',
    'lev': 'Nivel de logging que se configura.',
    '_inner_func': 'Función de bajo nivel que se ejecuta dentro del wrapper.',
    'on': 'Indica si el mecanismo debe activarse.',
    'taguid': 'Identificador UID de la etiqueta NFC.',
    'w_uid': 'UID de la etiqueta NFC que se actualiza.',
    'w_color': 'Color que se registra en el historial de la etiqueta.',
    'k': 'Clave del registro que se consulta.',
    'm': 'Mapa de historial que contiene los registros.',
}


def humanize(name):
    return name.strip('_').replace('_', ' ')


def description(name):
    subject = humanize(name)
    if name.startswith(('get_', 'read', 'map_get', 'timestamp', 'to_datetime')):
        return f'Obtiene el valor gestionado por {subject}.'
    if name.startswith(('set_', 'write_', 'save_', 'load_')):
        return f'Actualiza o persiste el valor gestionado por {subject}.'
    if name.startswith(('init', 'connect')):
        return f'Inicializa la configuración y los recursos de {subject}.'
    if name.startswith(('publish_', 'mqtt_callback', 'callback')):
        return f'Procesa o publica los datos asociados a {subject}.'
    if name.startswith(('thread_', '_worker', '_monitor')):
        return f'Ejecuta el ciclo de servicio de {subject}.'
    if name.startswith(('move', 'park', 'start_', 'end_', 'process')):
        return f'Ejecuta la operación de control {subject}.'
    if name.startswith('_'):
        return f'Aplica la transformación interna {subject}.'
    return f'Ejecuta la operación {subject}.'


def return_text(name, source):
    if name.startswith(('get_', 'read', 'map_get', 'timestamp', 'to_datetime', '_get_', '_to_', '_field_', '_escape_', '_format_', '_payload_', '_common_', 'math_mean')):
        return 'Valor calculado, convertido o recuperado por la función.'
    if 'return' not in source or name.startswith(('set_', 'init', 'connect', 'publish_', 'write_', 'save_', 'load_', 'thread_', 'callback', 'mqtt_callback', 'update_', 'start_', 'end_', 'move', 'park', 'beep', 'process')):
        return 'None.'
    return 'Resultado de la operación, o ``None`` cuando no hay un valor que devolver.'


def make_doc(node, source):
    args = [a.arg for a in node.args.args + node.args.kwonlyargs if a.arg != 'self']
    lines = [description(node.name), '']
    if args:
        lines += ['Args:']
        for arg in args:
            meaning = ARGUMENTS.get(arg, f'Valor asociado a {humanize(arg)}.')
            lines.append(f'  {arg}: {meaning}')
        lines.append('')
    lines += ['Returns:', f'  {return_text(node.name, source)}']
    return lines


def rewrite(path):
    with open(path, 'r', encoding='utf-8', newline='') as handle:
        source = handle.read()
    tree = ast.parse(source, filename=path)
    lines = source.splitlines(keepends=True)
    replacements = []
    for node in ast.walk(tree):
        if not isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)) or not node.body:
            continue
        first = node.body[0]
        if not isinstance(first, ast.Expr) or not isinstance(first.value, ast.Constant) or not isinstance(first.value.value, str):
            continue
        old = first.value.value
        if not any(marker in old for marker in TEMPLATE_MARKERS):
            continue
        indent = ' ' * first.col_offset
        doc = '"""' + '\n' + '\n'.join(indent + line if line else '' for line in make_doc(node, source)) + '\n' + indent + '"""'
        replacements.append((first.lineno - 1, first.col_offset, first.end_lineno - 1, first.end_col_offset, doc))
    for start_line, start_col, end_line, end_col, replacement in reversed(replacements):
        start = sum(len(line) for line in lines[:start_line]) + start_col
        end = sum(len(line) for line in lines[:end_line]) + end_col
        source = source[:start] + replacement + source[end:]
    if replacements:
        with open(path, 'w', encoding='utf-8', newline='') as handle:
            handle.write(source)
    return len(replacements)


root = os.path.dirname(os.path.abspath(__file__))
paths = [os.path.join(root, 'FactoryMain.py')]
paths.extend(os.path.join(root, 'lib', name) for name in os.listdir(os.path.join(root, 'lib')) if name.endswith('.py'))
total = 0
for path in paths:
    total += rewrite(path)
print(f'Docstrings actualizados: {total}')