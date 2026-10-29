namespace Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Quem consome o resultado da verificação. Conjunto fechado: cada origem tem a própria fila de
/// resultados, e um valor desconhecido é recusado em vez de caminhar para uma fila inventada.
/// </summary>
public enum DestinoDoResultado
{
    /// <summary>Os documentos do candidato, tratados pelo módulo Seleção. É o valor de quem não informa destino.</summary>
    Selecao = 0,

    /// <summary>O documento de designação dos gestores de acessos, tratado pelo módulo Autorização.</summary>
    Autorizacao = 1,
}
