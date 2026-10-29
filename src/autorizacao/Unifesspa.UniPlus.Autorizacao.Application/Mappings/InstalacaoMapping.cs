namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Mapeamento <see cref="Instalacao"/> para <see cref="InstalacaoDto"/>, para a trilha e para a auditoria.</summary>
public static class InstalacaoMapping
{
    public static InstalacaoDto ToDto(Instalacao i)
    {
        ArgumentNullException.ThrowIfNull(i);

        bool aguardandoCodigo = i.Situacao == SituacaoDaInstalacao.AguardandoCodigo;

        return new InstalacaoDto(
            i.Id,
            i.Situacao.ToCodigo(),
            i.NumeroDoDocumento,
            [.. i.Gestores.Select(g => new IdentidadeDto(g.Emissor, g.Sub))],
            i.IniciadaEm,
            aguardandoCodigo ? i.CodigoExpiraEm : null,
            aguardandoCodigo ? Instalacao.MaximoDeTentativas - i.Tentativas : null);
    }

    public static TentativaDeInstalacaoDto ToDto(TentativaDeInstalacao t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new TentativaDeInstalacaoDto(
            t.Id, t.InstalacaoId, new IdentidadeDto(t.AutorEmissor, t.AutorSub), t.Instante, t.Resultado.ToCodigo());
    }
}
