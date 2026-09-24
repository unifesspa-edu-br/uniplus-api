namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;

/// <summary>Tipos de etapa do cadastro para testes da Configuração.</summary>
internal static class TipoEtapaDeTeste
{
    /// <summary>
    /// O tipo com nota de origem no ENEM. O atributo só vem da carga do cadastro, e nenhuma
    /// operação do domínio o liga; aqui ele é ligado como a migration faz, sem passar pela API.
    /// </summary>
    public static TipoEtapa NotaDoEnem()
    {
        TipoEtapa tipo = TipoEtapa.Criar("NOTA_ENEM", "Nota do ENEM", null, true, true).Value!;
        typeof(TipoEtapa).GetProperty(nameof(TipoEtapa.NotaDeOrigemNoEnem))!.SetValue(tipo, true);
        return tipo;
    }
}
