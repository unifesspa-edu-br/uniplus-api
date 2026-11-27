namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que as partes que a leitura recusou vinculam no catálogo, para que o vínculo novo a dado desativado ou sem a aprovação da
/// proteção de dados saia junto das demais recusas. Entram o dado de cada campo e de cada documento recusados, como veio no
/// pedido, e tudo o que os campos e os documentos que a leitura aceitou em grupo recusado citam. O que uma regra recusada cita
/// fica de fora: depende do que foi recusado.
/// </summary>
internal static class VinculosDasPartesNaoLidas
{
    public static VinculosDeFatos De(ConteudoLido conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        return VinculosDeFatos.Unir(
        [
            VinculosDeFatos.De(
                conteudo.PartesNaoLidas.Campos.Select(static c => c.FatoCodigo).Concat(conteudo.PartesNaoLidas.FatosPorOpcaoDeDocumentos), []),
            .. conteudo.Grupos.CamposDeGruposNaoLidos.Select(static c => c.Campo.Vinculos()),
            .. conteudo.Documentos.DocumentosDeGruposNaoLidos.Select(static d => d.Documento.Vinculos()),
        ]);
    }
}
