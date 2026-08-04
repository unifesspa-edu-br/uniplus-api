namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ProcessoSeletivoConfiguration : IEntityTypeConfiguration<ProcessoSeletivo>
{
    private const int ReferenciaTemporalFatosTipoMaxLength = 20;

    public void Configure(EntityTypeBuilder<ProcessoSeletivo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("processos_seletivos");
        builder.HasKey(p => p.Id);
        // Chave Guid v7 gerada no domínio (EntityBase): ValueGeneratedNever
        // força o EF a tratar a chave como fornecida pela aplicação. Sem isso,
        // ao reconfigurar o agregado tracked (substituir filhos com Guid v7 já
        // preenchido), o EF marcaria os filhos novos como Modified → UPDATE de
        // linhas nunca inseridas. Convenção do repo (ver UnidadeIdentificadorHistorico).
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Nome).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Tipo).HasConversion<int>().IsRequired();
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        // Story #851 §3.4: NOT NULL, exigido na criação — sem produção, migration direta.
        builder.Property(p => p.OrigemCandidatos).HasConversion<int>().IsRequired();

        // Issue #849 (CA-04 da Feature #40): quem responde pelo certame — NOT NULL, exigido
        // na criação, imutável depois. Escalar de topo sem FK cross-schema (ADR-0061) + owned
        // type snapshot-copy, maxlengths espelhando UnidadeConfiguration.
        builder.Property(p => p.UnidadeAdministradoraOrigemId)
            .IsRequired()
            .HasComment("Id da Unidade administradora em Organização Institucional (ADR-0061, sem FK cross-schema) — congelado na criação, imutável.");

        builder.OwnsOne(p => p.UnidadeAdministradora, u =>
        {
            u.Property(x => x.Sigla).HasColumnName("unidade_administradora_sigla").HasMaxLength(50).IsRequired()
                .HasComment("Snapshot-copy da sigla da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
            u.Property(x => x.Slug).HasColumnName("unidade_administradora_slug").HasMaxLength(64).IsRequired()
                .HasComment("Snapshot-copy do slug da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
            u.Property(x => x.Nome).HasColumnName("unidade_administradora_nome").HasMaxLength(250).IsRequired()
                .HasComment("Snapshot-copy do nome da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
            u.Property(x => x.Tipo).HasColumnName("unidade_administradora_tipo").HasMaxLength(30).IsRequired()
                .HasComment("Snapshot-copy do tipo organizacional da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
        });
        builder.Navigation(p => p.UnidadeAdministradora).IsRequired();

        // Coleções filhas do agregado: entidades próprias com FK para a raiz
        // (nunca owned types).
        builder.HasMany(p => p.Etapas)
            .WithOne()
            .HasForeignKey(e => e.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.OfertaAtendimento)
            .WithOne()
            .HasForeignKey<OfertaAtendimentoEspecializado>(o => o.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.DistribuicaoVagas)
            .WithOne()
            .HasForeignKey(d => d.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.BonusRegional)
            .WithOne()
            .HasForeignKey<ConfiguracaoBonusRegional>(b => b.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.CriteriosDesempate)
            .WithOne()
            .HasForeignKey(c => c.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Classificacao)
            .WithOne()
            .HasForeignKey<ConfiguracaoClassificacao>(c => c.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cronograma de fases (Story #851) — 1..*, mesmo padrão de Etapas/DistribuicaoVagas.
        builder.HasMany(p => p.CronogramaFases)
            .WithOne()
            .HasForeignKey(f => f.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Documentos exigidos (Story #554) — 0..*, mesmo padrão de Etapas/CronogramaFases.
        builder.HasMany(p => p.DocumentosExigidos)
            .WithOne()
            .HasForeignKey(d => d.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Árvore de satisfação (Story #920) — 0..*, coleção PLANA (todos os nós, não só
        // raízes; ver NoExigenciaConfiguration). Cascade: FK obrigatória, mesmo padrão de
        // DocumentosExigidos — Clear()+Add() no replace-all do agregado já prova orphan-delete.
        builder.HasMany(p => p.NosExigencia)
            .WithOne()
            .HasForeignKey(n => n.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Grafo de coleta de fatos (Story #926) — quais fatos o processo coleta, em que ordem e
        // sob qual pré-condição. Cascade pelo mesmo motivo dos documentos exigidos: a FK é
        // obrigatória e o agregado substitui a coleção por inteiro.
        builder.HasMany(p => p.FatosColetados)
            .WithOne()
            .HasForeignKey(f => f.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.FatosColetados)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Regras de derivação (Story #927) — mesma disciplina de FatosColetados: FK obrigatória,
        // cascade, substituição por inteiro pelo agregado.
        builder.HasMany(p => p.RegrasDerivacao)
            .WithOne()
            .HasForeignKey(c => c.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.RegrasDerivacao)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ReferenciaTemporalFatos (Story #554, PR #896) — VO 0..1 sem identidade própria,
        // owned inline em processos_seletivos (nunca entidade filha própria — ela não tem
        // Id nem ciclo de vida próprio, diferente das coleções acima).
        builder.OwnsOne(p => p.ReferenciaTemporalFatos, referencia =>
        {
            referencia.Property(r => r.Tipo)
                .HasColumnName("referencia_temporal_fatos_tipo")
                .HasConversion(ReferenciaTipoConverter)
                .HasMaxLength(ReferenciaTemporalFatosTipoMaxLength);
            referencia.Property(r => r.Data).HasColumnName("referencia_temporal_fatos_data");
            referencia.Property(r => r.FaseId).HasColumnName("referencia_temporal_fatos_fase_id");
        });

        // A sessão editorial (ADR-0110 D3) — 1:1, como as demais filhas singulares. Ela é
        // efêmera (apagada no fechamento e no descarte) e não é evidência forense: a
        // auditoria com peso jurídico vive na VersaoConfiguracao, que é append-only.
        builder.HasOne(p => p.Rascunho)
            .WithOne()
            .HasForeignKey<RascunhoRetificacao>(r => r.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Etapas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.DistribuicaoVagas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.CriteriosDesempate)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.CronogramaFases)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.DocumentosExigidos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.NosExigencia)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // RaizesDeExigencia é projeção EM MEMÓRIA de NosExigencia (NoPaiId == null) — sem
        // isto, o EF a descobre por convenção como uma SEGUNDA coleção de NoExigencia
        // (mesmo tipo de retorno IEnumerable<NoExigencia>) e cria uma FK-sombra duplicada
        // (processo_seletivo_id1) para desambiguar.
        builder.Ignore(p => p.RaizesDeExigencia);
    }

    private static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<ReferenciaTipo, string?> ReferenciaTipoConverter =
        new(
            tipo => tipo == ReferenciaTipo.Nenhuma ? null : tipo.ToCodigo(),
            codigo => ReferenciaTipoCodigo.FromCodigo(codigo));
}
