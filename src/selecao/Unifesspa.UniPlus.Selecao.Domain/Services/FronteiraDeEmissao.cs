namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Linq;
using System.Text.Json;

using Entities;

using Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

/// <summary>
/// A fronteira ativa de emissão da árvore de satisfação: a máscara <c>emissionBlocked</c> sob a
/// fronteira de disponibilidade e a emissão das consequências e orientações que vigoram, raiz→baixo.
/// </summary>
internal static class FronteiraDeEmissao
{
    /// <summary>
    /// O predicado recursivo <c>emissionBlocked</c> (Story #928, §6), fonte única da máscara de
    /// emissão sob a fronteira de disponibilidade. Preenche <paramref name="suprimidos"/> com os ids
    /// dos nós cuja emissão é suprimida, num pós-percurso (o resultado de um grupo depende do dos
    /// filhos). Regras: folha em pendente/indeterminado é <c>emissionBlocked</c> sse BLOQUEADA; grupo
    /// em pendente/indeterminado é <c>emissionBlocked</c> sse a sua fronteira decisiva (filhos
    /// não-satisfeitos e não-não-aplicáveis) é não vazia e TODOS os seus membros são
    /// <c>emissionBlocked</c> recursivamente; <c>IMPOSSIVEL</c> decidido nunca é suprimido (emite o
    /// inevitável); satisfeito/não-aplicável é irrelevante para a máscara. Sem latch — recalculado a
    /// cada resolução, então o desbloqueio reavalia sozinho.
    /// </summary>
    internal static bool CalcularEmissionBlocked(
        NoExigencia no,
        IReadOnlyDictionary<Guid, EstadoSatisfacao> estados,
        IReadOnlySet<Guid> bloqueadas,
        HashSet<Guid> suprimidos,
        bool naInstancia = false)
    {
        // A subárvore repetida tem estados por ocorrência: a máscara dela é calculada em cada
        // instância, sobre os estados dela, e não entra no conjunto global.
        if (no.RepetePorEntidade is not null && !naInstancia)
        {
            return false;
        }

        EstadoSatisfacao estado = estados[no.Id];

        if (no.Tipo == TipoNo.Folha)
        {
            // Uma folha só é emissionBlocked em pendente/indeterminado (a bloqueada projeta
            // indeterminado). Satisfeita/não-aplicável é irrelevante para a máscara.
            bool folhaBloqueada = estado is not (EstadoSatisfacao.Satisfeito or EstadoSatisfacao.NaoAplicavel)
                && bloqueadas.Contains(no.DocumentoExigido!.Id);
            if (folhaBloqueada)
            {
                suprimidos.Add(no.Id);
            }

            return folhaBloqueada;
        }

        // Grupo: percorre SEMPRE todos os filhos primeiro, para preencher o conjunto na subárvore
        // inteira — inclusive quando o próprio grupo já é terminal (IMPOSSIVEL/SATISFEITO). Uma folha
        // bloqueada sob um grupo IMPOSSIVEL continua suprimida, senão o grupo emitiria o inevitável
        // (correto) mas também a folha bloqueada (errado).
        List<bool> decisivosBloqueados = [];
        foreach (NoExigencia filho in no.Filhos)
        {
            bool filhoBloqueado = CalcularEmissionBlocked(filho, estados, bloqueadas, suprimidos, naInstancia);
            if (estados[filho.Id] is not (EstadoSatisfacao.Satisfeito or EstadoSatisfacao.NaoAplicavel))
            {
                decisivosBloqueados.Add(filhoBloqueado);
            }
        }

        // O próprio grupo só é emissionBlocked em pendente/indeterminado com a fronteira decisiva não
        // vazia e toda bloqueada; IMPOSSIVEL decidido emite o inevitável, satisfeito/NA é irrelevante.
        bool grupoBloqueado = estado is not (EstadoSatisfacao.Impossivel
            or EstadoSatisfacao.Satisfeito or EstadoSatisfacao.NaoAplicavel)
            && decisivosBloqueados.Count > 0 && decisivosBloqueados.All(static b => b);
        if (grupoBloqueado)
        {
            suprimidos.Add(no.Id);
        }

        return grupoBloqueado;
    }

    /// <summary>
    /// Emissão de consequência por fronteira ativa, raiz→baixo — nunca pai e filho emitindo ao
    /// mesmo tempo. Chamada uma vez por raiz de <see cref="ArvoreExigenciasCongelada.Raizes"/>.
    /// Dispatcher: se <paramref name="no"/> é a raiz de uma subárvore repetida (Story #922),
    /// desdobra por instância (cada uma com seu próprio dicionário de estados descendentes,
    /// nunca o global) antes de aplicar a regra normal de fronteira.
    /// </summary>
    internal static void Emitir(
        NoExigencia no,
        EstadoSatisfacao estado,
        IReadOnlyDictionary<Guid, EstadoSatisfacao> estados,
        IReadOnlySet<Guid> nosEmissaoSuprimida,
        List<ConsequenciaEmitida> consequenciasVigentes,
        List<PendenciaDeOrientacao> pendenciasDeOrientacao,
        IReadOnlyDictionary<Guid, List<InstanciaResolvida>> instanciasResolvidasPorNo)
    {
        if (estado is EstadoSatisfacao.Satisfeito or EstadoSatisfacao.NaoAplicavel)
        {
            // Nó satisfeito/não-aplicável suprime toda a subárvore — nada abaixo vigora.
            return;
        }

        // Máscara de emissão sob a fronteira (§6): um nó emissionBlocked não emite — nem a própria
        // consequência de OU/N-de opaco, nem desce para os filhos. Ao desbloquear, reavalia.
        if (nosEmissaoSuprimida.Contains(no.Id))
        {
            return;
        }

        if (no.RepetePorEntidade is not null)
        {
            // Cada instância pendente/indeterminada/impossível é uma obrigação DISTINTA do
            // candidato (ex.: "PJ 2 ainda deve os extratos") — emite por instância, tagueada com
            // entidade_id (consequências E orientações, senão duas instâncias com filhos
            // pendentes diferentes de um MESMO grupo OU repetido ficariam indistinguíveis numa
            // lista só), usando os estados DESCENDENTES resolvidos para aquela instância (nunca
            // o dicionário global, que não os contém).
            foreach (InstanciaResolvida instancia in instanciasResolvidasPorNo.GetValueOrDefault(no.Id, []))
            {
                if (!EhPendenteOuPior(instancia.Estado))
                {
                    continue;
                }

                List<ConsequenciaEmitida> consequenciasDaInstancia = [];
                List<PendenciaDeOrientacao> pendenciasDaInstancia = [];
                if (instancia.NosEmissaoSuprimida.Contains(no.Id))
                {
                    continue;
                }

                EmitirNormal(
                    no, instancia.Estado, instancia.EstadosDescendentes, instancia.NosEmissaoSuprimida,
                    consequenciasDaInstancia, pendenciasDaInstancia, instanciasResolvidasPorNo);

                consequenciasVigentes.AddRange(
                    consequenciasDaInstancia.Select(c => c with { EntidadeId = instancia.EntidadeId }));
                pendenciasDeOrientacao.AddRange(
                    pendenciasDaInstancia.Select(p => p with { EntidadeId = instancia.EntidadeId }));
            }

            return;
        }

        EmitirNormal(no, estado, estados, nosEmissaoSuprimida, consequenciasVigentes, pendenciasDeOrientacao, instanciasResolvidasPorNo);
    }

    internal static void EmitirNormal(
        NoExigencia no,
        EstadoSatisfacao estado,
        IReadOnlyDictionary<Guid, EstadoSatisfacao> estados,
        IReadOnlySet<Guid> nosEmissaoSuprimida,
        List<ConsequenciaEmitida> consequenciasVigentes,
        List<PendenciaDeOrientacao> pendenciasDeOrientacao,
        IReadOnlyDictionary<Guid, List<InstanciaResolvida>> instanciasResolvidasPorNo)
    {
        switch (no.Tipo)
        {
            case TipoNo.Folha:
                if (no.DocumentoExigido!.ConsequenciaIndeferimento is { } consequenciaFolha)
                {
                    consequenciasVigentes.Add(new ConsequenciaEmitida(no.Id, TipoNo.Folha, consequenciaFolha));
                }

                break;

            case TipoNo.GrupoOu:
                if (no.Consequencia is { } consequenciaGrupo)
                {
                    consequenciasVigentes.Add(new ConsequenciaEmitida(no.Id, TipoNo.GrupoOu, consequenciaGrupo));
                }

                // Opaco: os filhos NÃO emitem consequência individual — só orientação. Um filho
                // emissionBlocked (fronteira não alcançada) não gera nem orientação — não se pede o
                // que ainda não está disponível.
                foreach (NoExigencia filho in no.Filhos.Where(filho =>
                    EhPendenteOuPior(estados[filho.Id]) && !nosEmissaoSuprimida.Contains(filho.Id)))
                {
                    pendenciasDeOrientacao.Add(new PendenciaDeOrientacao(filho.Id));
                }

                break;

            case TipoNo.GrupoE:
                // Transparente: não emite consequência própria — desce para os filhos não
                // satisfeitos e não-não-aplicáveis, cada um emitindo pela sua regra (via
                // EmitirFronteira, não EmitirFronteiraNormal — um filho pode, ele mesmo, ser a
                // raiz de uma subárvore repetida).
                foreach (NoExigencia filho in no.Filhos)
                {
                    EstadoSatisfacao estadoFilho = estados[filho.Id];
                    if (EhPendenteOuPior(estadoFilho))
                    {
                        Emitir(filho, estadoFilho, estados, nosEmissaoSuprimida, consequenciasVigentes, pendenciasDeOrientacao, instanciasResolvidasPorNo);
                    }
                }

                break;
        }
    }

    internal static bool EhPendenteOuPior(EstadoSatisfacao estado) =>
        estado is EstadoSatisfacao.Pendente or EstadoSatisfacao.Indeterminado or EstadoSatisfacao.Impossivel;
}
